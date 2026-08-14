using System.Net.Http.Json;
using Hato.Modules.Breeding.Contracts;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

/// <summary>
/// The Fase 3.5 retrospective left out the read-side of birthings: once a litter is
/// registered from the field-app or the panel, there was no way to see it again
/// (no list, no detail). These tests pin a single GET endpoint that returns the
/// camada with its offspring — the piece the panel needs to add a "Partos" tab and
/// the piece a user needs to find the birth they just recorded.
/// </summary>
public class ListBirthingsApiTests(BreedingApiFactory factory) : IClassFixture<BreedingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetBirthings_ReturnsRegisteredBirthWithOffspring()
    {
        // Arrange — species, dam, and a litter with three live-born piglets (two with
        // a weight, one without) registered via the public API.
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species",
            new { name = $"Porcino-List-{Guid.NewGuid():N}", gestationDays = 114 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals",
            new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate = new DateOnly(2026, 8, 14),
            difficulty = "Normal",
            bornAlive = 3,
            bornDead = 0,
            mummified = 0,
            offspring = new object[]
            {
                new { childId = Guid.NewGuid(), farmTag = (string?)null, sex = "F", birthWeightKg = 1.420m },
                new { childId = Guid.NewGuid(), farmTag = (string?)null, sex = "M", birthWeightKg = 1.680m },
                new { childId = Guid.NewGuid(), farmTag = (string?)null, sex = "F", birthWeightKg = (decimal?)null },
            }
        });
        birthingResponse.EnsureSuccessStatusCode();
        var birthing = await birthingResponse.Content.ReadFromJsonAsync<BirthingResponseDto>();
        Assert.NotNull(birthing);

        // Act — read back the list the user has no other way to see today.
        var listResponse = await _client.GetAsync("/api/v1/breeding/birthings");

        // Assert
        listResponse.EnsureSuccessStatusCode();
        var items = await listResponse.Content.ReadFromJsonAsync<List<BirthingListItemDto>>();
        Assert.NotNull(items);

        var found = Assert.Single(items!, b => b.Id == birthing!.Id);
        Assert.Equal(new DateOnly(2026, 8, 14), found.BirthDate);
        Assert.Equal(3, found.BornAlive);
        Assert.Equal(0, found.BornDead);
        Assert.Equal(0, found.Mummified);
        Assert.Equal(damId, found.DamId);

        // The nested offspring must round-trip the weights the operator typed, including
        // the (legitimate) null for the one not weighed, so the panel can display the
        // initial weight column the user just asked for.
        Assert.NotNull(found.Offspring);
        Assert.Equal(3, found.Offspring!.Count);

        // Assert against the multiset of weights, not a dictionary keyed by sex — the
        // test seeded two F offspring, which would duplicate the dictionary key.
        var weights = found.Offspring.Select(o => o.BirthWeightKg).ToList();
        Assert.Contains(1.420m, weights);
        Assert.Contains(1.680m, weights);
        Assert.Contains(weights, w => w == null);
    }

    [Fact]
    public async Task GetBirthings_WhenNone_ReturnsEmptyList()
    {
        var listResponse = await _client.GetAsync("/api/v1/breeding/birthings");

        listResponse.EnsureSuccessStatusCode();
        var items = await listResponse.Content.ReadFromJsonAsync<List<BirthingListItemDto>>();
        Assert.NotNull(items);
        Assert.Empty(items!);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record BirthingResponseDto(Guid Id);
}
