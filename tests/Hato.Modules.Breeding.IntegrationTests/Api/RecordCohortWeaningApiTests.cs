using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Infrastructure.Persistence;
using Hato.Modules.Livestock.Application.MortalityCauses;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

/// <summary>
/// docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4: weaning is recorded at the cohort level.
/// Bug A1 (post-mortem of Fase 3.5, 2026-08-06): the handler used to pass
/// <c>birthing.BornAlive</c> as the weaned count, ignoring preweaning deaths — which
/// is exactly the data point 3.5a.3 was built to capture. These tests pin the fix.
/// </summary>
public class RecordCohortWeaningApiTests(BreedingApiFactory factory) : IClassFixture<BreedingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync(string? name = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/species",
            new { name = name ?? $"Porcino-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    /// <summary>
    /// Seeds the lactation parameters on the species. The HTTP command does not yet
    /// accept these fields (BLOQUE B3 of the Fase 3.5 fix list). The test reaches into
    /// the DbContext so the test does not depend on the endpoint, only on the
    /// underlying invariant the handler is meant to honour.
    /// </summary>
    private async Task<Guid> CreateSpeciesWithLactationAsync(int daysOfLactation)
    {
        var speciesId = await CreateSpeciesAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();
        var species = await dbContext.Species.FirstAsync(s => s.Id == speciesId);
        species.UpdateLactation(daysOfLactation, cohortWindowDays: null);
        await dbContext.SaveChangesAsync();

        return speciesId;
    }

    private async Task<Guid> CreateActiveMortalityCauseAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/mortality-causes",
            new CreateMortalityCauseRequest($"Causa-Test-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateDamAsync(Guid speciesId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private record CreatedId(Guid Id);

    /// <summary>
    /// Bug A1 (P1-1 of the Fase 3.5 retrospective): the handler assumed zero
    /// preweaning mortality and persisted <c>BornAlive</c> as the weaned count, which
    /// silently inflated every weaning event from the day 3.5a.4 went in. With the
    /// fix, the weaned count for each birthing equals BornAlive minus the preweaning
    /// disposals of that litter.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTwoPigletsDiedBeforeWeaning_PersistsWeanedCountAsBornAliveMinusTwo()
    {
        var speciesId = await CreateSpeciesWithLactationAsync(daysOfLactation: 24);
        var damId = await CreateDamAsync(speciesId);
        var causeId = await CreateActiveMortalityCauseAsync();

        const int bornAlive = 10;
        var birthDate = new DateOnly(2026, 8, 1);

        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate,
            difficulty = "Normal",
            bornAlive,
            offspring = Enumerable.Range(0, bornAlive)
                .Select(i => new
                {
                    childId = Guid.NewGuid(),
                    farmTag = $"LECHON-{i:D2}",
                    sex = i % 2 == 0 ? "M" : "F",
                    categoryId = (Guid?)null,
                })
                .ToArray(),
        });
        birthingResponse.EnsureSuccessStatusCode();
        var birthing = await birthingResponse.Content.ReadFromJsonAsync<BirthingDto>();
        Assert.NotNull(birthing);
        Assert.Equal(bornAlive, birthing!.BornAlive);
        Assert.NotNull(birthing.NursingCohortId);

        // Pull the 10 offspring and dispose of the first two as if they died before
        // weaning (this is the data point 3.5a.3 was built to capture).
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();
        var offspring = await dbContext.Animals
            .Where(a => a.BirthingId == birthing.Id)
            .OrderBy(a => a.CreatedAt)
            .Take(2)
            .ToListAsync();
        Assert.Equal(2, offspring.Count);

        var now = DateTimeOffset.UtcNow;
        foreach (var deadPiglet in offspring)
        {
            dbContext.AnimalEvents.Add(AnimalEvent.Create(
                deadPiglet.Id, EventType.Disposal, now, "capataz", "{}", causeId: causeId));
        }
        await dbContext.SaveChangesAsync();

        // Act: wean the cohort.
        var weaningResponse = await _client.PostAsJsonAsync(
            $"/api/v1/breeding/cohorts/{birthing.NursingCohortId}/wean",
            new { });
        weaningResponse.EnsureSuccessStatusCode();

        // Assert: the per-birthing WeanedCount reflects the preweaning deaths.
        // Read straight from the DbContext — the project has no GET endpoint for
        // a single Birthing, and adding one solely for this test would be a
        // surface added without a consumer. The DbContext is the source of truth.
        using var assertScope = factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<BreedingDbContext>();
        var weaned = await assertContext.Birthings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == birthing.Id);

        Assert.NotNull(weaned);
        Assert.Equal(bornAlive - offspring.Count, weaned!.WeanedCount);
        Assert.NotNull(weaned.WeanedAt);
    }

    /// <summary>
    /// Sanity baseline: when no piglet died, the weaned count equals BornAlive.
    /// Pairs with the bug test above to prove the change is not a constant.
    /// </summary>
    [Fact]
    public async Task Handle_WithNoPreweaningDeaths_PersistsWeanedCountAsBornAlive()
    {
        var speciesId = await CreateSpeciesWithLactationAsync(daysOfLactation: 24);
        var damId = await CreateDamAsync(speciesId);

        const int bornAlive = 7;
        var birthDate = new DateOnly(2026, 8, 5);

        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate,
            difficulty = "Normal",
            bornAlive,
            offspring = Enumerable.Range(0, bornAlive)
                .Select(i => new
                {
                    childId = Guid.NewGuid(),
                    farmTag = $"SANOS-{i:D2}",
                    sex = "F",
                    categoryId = (Guid?)null,
                })
                .ToArray(),
        });
        birthingResponse.EnsureSuccessStatusCode();
        var birthing = await birthingResponse.Content.ReadFromJsonAsync<BirthingDto>();
        Assert.NotNull(birthing);

        var weaningResponse = await _client.PostAsJsonAsync(
            $"/api/v1/breeding/cohorts/{birthing!.NursingCohortId}/wean",
            new { });
        weaningResponse.EnsureSuccessStatusCode();

        using var assertScope = factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<BreedingDbContext>();
        var weaned = await assertContext.Birthings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == birthing.Id);

        Assert.NotNull(weaned);
        Assert.Equal(bornAlive, weaned!.WeanedCount);
    }
}