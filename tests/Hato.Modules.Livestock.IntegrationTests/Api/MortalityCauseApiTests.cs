using System.Net;
using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Application.MortalityCauses;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5a.3: causa distingue "madre que aplasta" de "madre con mala leche" — sin causa la
/// mortalidad es sólo un número. Covers individual and group disposals with cause, the
/// referential check, and the pre-weaning mortality-by-mother aggregate.
/// </summary>
public class MortalityCauseApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Porcino-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalAsync(Guid speciesId)
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

    private async Task<Guid> ActiveCauseIdAsync()
    {
        var response = await _client.GetAsync("/api/v1/mortality-causes");
        response.EnsureSuccessStatusCode();
        var causes = await response.Content.ReadFromJsonAsync<List<MortalityCauseDto>>();
        return causes!.First(c => c.IsActive).Id;
    }

    [Fact]
    public async Task Catalog_IsSeededWithTheStandardSixCauses()
    {
        var response = await _client.GetAsync("/api/v1/mortality-causes");
        response.EnsureSuccessStatusCode();
        var causes = await response.Content.ReadFromJsonAsync<List<MortalityCauseDto>>();

        Assert.NotNull(causes);
        Assert.True(causes!.Count >= 6);
        Assert.Contains(causes, c => c.Name == "Aplastamiento");
        Assert.Contains(causes, c => c.Name == "Desconocida");
    }

    [Fact]
    public async Task CreateCause_ThenDeactivate_HidesItFromTheDefaultList()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/mortality-causes", new CreateMortalityCauseRequest($"Causa-{Guid.NewGuid():N}"));
        createResponse.EnsureSuccessStatusCode();
        var causeId = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivateResponse = await _client.DeleteAsync($"/api/v1/mortality-causes/{causeId}");
        deactivateResponse.EnsureSuccessStatusCode();

        var listResponse = await _client.GetAsync("/api/v1/mortality-causes");
        var causes = await listResponse.Content.ReadFromJsonAsync<List<MortalityCauseDto>>();

        Assert.DoesNotContain(causes!, c => c.Id == causeId);
    }

    [Fact]
    public async Task RecordIndividualDisposal_WithCause_Succeeds()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var causeId = await ActiveCauseIdAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"disposalType\":\"Death\"}",
            causeId,
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RecordIndividualDisposal_WithNonexistentCause_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var response = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"disposalType\":\"Death\"}",
            causeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordIndividualDisposal_WithInactiveCause_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/mortality-causes", new CreateMortalityCauseRequest($"Causa-{Guid.NewGuid():N}"));
        createResponse.EnsureSuccessStatusCode();
        var causeId = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;
        (await _client.DeleteAsync($"/api/v1/mortality-causes/{causeId}")).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"disposalType\":\"Death\"}",
            causeId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordGroupMortality_WithCause_Succeeds()
    {
        var speciesId = await CreateSpeciesAsync();
        var causeId = await ActiveCauseIdAsync();

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"Engorde-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId,
            trackingMode = TrackingMode.Headcount,
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        for (var i = 0; i < 3; i++)
        {
            var animalId = await CreateAnimalAsync(speciesId);
            (await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new
            {
                animalId,
                joinedAt = new DateOnly(2026, 1, 1),
            })).EnsureSuccessStatusCode();
        }

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":1}",
            affectedCount = 1,
            causeId,
        });

        response.EnsureSuccessStatusCode();

        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        var count = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();
        Assert.Equal(2, count!.LiveHeadCount);
    }

    /// <summary>
    /// Seeds a mother with three piglets, kills two of them with a cause and leaves one
    /// alive, and checks the aggregate lands on the right mother with the right count —
    /// the "predestete por madre" test the plan requires (sec.4.6 will consume this).
    /// Written against the DbContext/ISender directly because no HTTP surface for this
    /// query exists yet (it has no consumer until 3.5b/4.6); the genealogy is set the same
    /// way Breeding's IAnimalRegistrationService does it internally.
    /// </summary>
    [Fact]
    public async Task PreweaningMortalityByMother_AggregatesDeathsCorrectly_AndIgnoresTheSurvivor()
    {
        var speciesId = await CreateSpeciesAsync();
        var causeId = await ActiveCauseIdAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var mother = Animal.Register(speciesId, Sex.Female);
        dbContext.Animals.Add(mother);

        var unrelatedMother = Animal.Register(speciesId, Sex.Female);
        dbContext.Animals.Add(unrelatedMother);

        var piglet1 = Animal.Register(speciesId, Sex.Male);
        var piglet2 = Animal.Register(speciesId, Sex.Female);
        var survivor = Animal.Register(speciesId, Sex.Male);
        var unrelatedDeath = Animal.Register(speciesId, Sex.Female);

        piglet1.SetGenealogy(mother.Id, null, null);
        piglet2.SetGenealogy(mother.Id, null, null);
        survivor.SetGenealogy(mother.Id, null, null);
        unrelatedDeath.SetGenealogy(unrelatedMother.Id, null, null);

        dbContext.Animals.AddRange(piglet1, piglet2, survivor, unrelatedDeath);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        dbContext.AnimalEvents.Add(AnimalEvent.Create(
            piglet1.Id, EventType.Disposal, now, "capataz", "{}", causeId: causeId));
        dbContext.AnimalEvents.Add(AnimalEvent.Create(
            piglet2.Id, EventType.Disposal, now, "capataz", "{}", causeId: causeId));
        dbContext.AnimalEvents.Add(AnimalEvent.Create(
            unrelatedDeath.Id, EventType.Disposal, now, "capataz", "{}", causeId: causeId));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var result = await sender.Send(new GetPreweaningMortalityByMotherQuery(mother.Id));

        var entry = Assert.Single(result);
        Assert.Equal(mother.Id, entry.MotherId);
        Assert.Equal(2, entry.DeathCount);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record LiveHeadCountDto(int LiveHeadCount);
}
