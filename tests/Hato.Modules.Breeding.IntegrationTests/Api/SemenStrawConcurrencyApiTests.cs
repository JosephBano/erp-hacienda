using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

/// <summary>
/// Two inseminations racing against the last remaining straw must not both succeed —
/// that would oversell inventory that doesn't exist. The xmin concurrency token on
/// SemenStraw makes the losing request fail instead of silently double-decrementing.
/// </summary>
public class SemenStrawConcurrencyApiTests(BreedingApiFactory factory) : IClassFixture<BreedingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterBreedingService_TwoConcurrentRequestsForLastStraw_OnlyOneSucceeds()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Bovino Straw Race", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var breedResponse = await _client.PostAsJsonAsync("/api/v1/breeds", new { speciesId, name = "Holstein Race" });
        breedResponse.EnsureSuccessStatusCode();
        var breedId = (await breedResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var strawResponse = await _client.PostAsJsonAsync("/api/v1/breeding/semen-straws", new
        {
            code = $"STRAW-RACE-{Guid.NewGuid():N}",
            bullName = "Toro Race",
            breedId,
            quantity = 1
        });
        strawResponse.EnsureSuccessStatusCode();
        var strawId = (await strawResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var dam1Response = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        dam1Response.EnsureSuccessStatusCode();
        var dam1Id = (await dam1Response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var dam2Response = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        dam2Response.EnsureSuccessStatusCode();
        var dam2Id = (await dam2Response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        Func<Guid, Task<HttpResponseMessage>> registerService = damId => _client.PostAsJsonAsync("/api/v1/breeding/services", new
        {
            damId,
            serviceType = "ArtificialInsemination",
            serviceDate = new DateOnly(2026, 1, 1),
            sireAnimalId = (Guid?)null,
            strawId,
        });

        var results = await Task.WhenAll(registerService(dam1Id), registerService(dam2Id));

        Assert.Single(results, r => r.IsSuccessStatusCode);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);

        var strawsResponse = await _client.GetAsync("/api/v1/breeding/semen-straws");
        strawsResponse.EnsureSuccessStatusCode();
        var straws = await strawsResponse.Content.ReadFromJsonAsync<List<SemenStrawDto>>();

        var straw = Assert.Single(straws!, s => s.Id == strawId);
        Assert.Equal(0, straw.CurrentQuantity);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record SemenStrawDto(Guid Id, int CurrentQuantity);
}
