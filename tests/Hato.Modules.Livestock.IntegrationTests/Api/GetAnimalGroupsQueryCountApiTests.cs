using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// docs/spec/feature-0001-admin-web-animal-groups/spec.md sec.8, riesgo "La agregación de
/// GetAnimalGroupsHandler introduce un N+1 si se rompe el batching. No falla ninguna prueba
/// funcional: sólo se pone lento." — mitigación declarada: "Prueba de integración que cuenta
/// consultas con DbCommandInterceptor: 50 grupos, &lt;=4 consultas."
///
/// GetAnimalGroupsHandler (see GetAnimalGroupQueries.cs) is documented as exactly 4
/// round-trips regardless of how many groups it returns: (1) groups + memberships,
/// (2) active-membership counts grouped by group id, (3) disposal totals grouped by group
/// id, (4) species names for the referenced species ids. If any of those turns into a
/// per-group query, this test catches it — a purely functional test (like
/// GetAnimalGroupsQuery_PopulatesLiveHeadCountAndSpeciesName in AnimalGroupApiTests.cs)
/// would stay green even though the handler got slow.
/// </summary>
public class GetAnimalGroupsQueryCountApiTests(QueryCountingApiFactory factory) : IClassFixture<QueryCountingApiFactory>
{
    private const int GroupCount = 50;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAnimalGroupsQuery_Over50Groups_ExecutesAtMostFourQueries()
    {
        // Arrange: seed 50 groups through the normal HTTP/ISender pipeline, which runs
        // against the app's own DI-registered DbContext (no interceptor attached). Each
        // group gets a species so SpeciesName has real work to resolve, and roughly a
        // third get an active membership so the aggregation joins fold in real rows
        // instead of empty sets.
        var speciesA = await CreateSpeciesAsync();
        var speciesB = await CreateSpeciesAsync();

        var groupIds = new List<Guid>();
        for (var i = 0; i < GroupCount; i++)
        {
            var speciesId = i % 2 == 0 ? speciesA : speciesB;
            var groupId = await CreateGroupOnlyAsync(speciesId, $"QueryCount Group {i:D2}-{Guid.NewGuid():N}");
            groupIds.Add(groupId);

            if (i % 3 == 0)
            {
                var animalId = await CreateAnimalAsync(speciesId);
                await SendAsync(new AddGroupMemberCommand(groupId, animalId, new DateOnly(2026, 1, 1)));
            }
        }

        // Act: run the handler under test through a *separate* LivestockDbContext wired
        // with the counting interceptor, so only its own SQL is measured — none of the
        // arrange traffic above went through this DbContext instance. Reset() is called
        // defensively in case the interceptor instance is ever reused across tests.
        var interceptor = new QueryCountingInterceptor();
        interceptor.Reset();

        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(factory.ConnectionStringForTests)
            .AddInterceptors(interceptor)
            .Options;

        List<AnimalGroupDto> groups;
        await using (var dbContext = new LivestockDbContext(options))
        {
            var handler = new GetAnimalGroupsHandler(dbContext);
            groups = await handler.Handle(new GetAnimalGroupsQuery(IncludeInactive: false), CancellationToken.None);
        }

        // Assert data correctness first: a handler that silently returns an empty list
        // (or omits the derived fields) would also pass a query-count-only assertion.
        var seeded = groups.Where(g => groupIds.Contains(g.Id)).ToList();
        Assert.Equal(GroupCount, seeded.Count);
        Assert.All(seeded, g => Assert.NotNull(g.SpeciesName));
        Assert.Contains(seeded, g => g.LiveHeadCount == 1);

        // Assert the batching contract from spec.md sec.8. If this fails, the
        // aggregation regressed into per-group queries (N+1); check for a per-group
        // .Include/loop that replaced the GroupBy + ToDictionaryAsync batch queries in
        // GetAnimalGroupsHandler (GetAnimalGroupQueries.cs).
        // Lower bound first: a detached or broken interceptor reports 0, which would
        // satisfy the <=4 upper bound vacuously and leave the N+1 guard dead. The
        // handler cannot answer this query without hitting the database at least once.
        Assert.True(interceptor.Count > 0,
            "El QueryCountingInterceptor contó 0 consultas: no quedó conectado al DbContext, " +
            "así que la cota de <=4 pasaría de forma vacua y no protegería contra el N+1.");

        Assert.True(interceptor.Count <= 4,
            $"GetAnimalGroupsQuery ejecutó {interceptor.Count} consultas SQL para {GroupCount} grupos " +
            "(se esperaban <=4). Esto indica que el batching de GetAnimalGroupsHandler se rompió y " +
            "reintrodujo un N+1 (ver docs/spec/feature-0001-admin-web-animal-groups/spec.md sec.8).");
    }

    // === Helpers, following AnimalGroupApiTests.cs precedent ===

    private async Task SendAsync(IRequest request)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(request);
    }

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Bovino-QC-{Guid.NewGuid():N}",
            gestationDays = 283,
        });
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

    private async Task<Guid> CreateGroupOnlyAsync(Guid speciesId, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name,
            description = (string?)null,
            speciesId = (Guid?)speciesId,
            trackingMode = TrackingMode.Individual,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private sealed record CreatedId(Guid Id);
}
