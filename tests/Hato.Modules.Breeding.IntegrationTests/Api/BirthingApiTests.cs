using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

/// <summary>
/// The Fase 2 exit criterion is "the calf was born inside the system with its
/// genealogy" — RecordBirthingCommand used to raise a domain event nobody ever
/// dispatched, so the offspring was never actually created. This proves it now is.
/// </summary>
public class BirthingApiTests(BreedingApiFactory factory) : IClassFixture<BreedingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordBirthing_WithOffspring_CreatesAnimalWithGenealogy()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/animal-categories", new { speciesId, name = "Ternera" });
        categoryResponse.EnsureSuccessStatusCode();
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate = new DateOnly(2026, 8, 1),
            difficulty = "Normal",
            bornAlive = 1,
            offspring = new[]
            {
                new { childId = Guid.NewGuid(), farmTag = "TERNERA-01", sex = "F", categoryId }
            }
        });
        birthingResponse.EnsureSuccessStatusCode();
        var birthing = await birthingResponse.Content.ReadFromJsonAsync<BirthingDto>();

        var animalsResponse = await _client.GetAsync("/api/v1/animals");
        animalsResponse.EnsureSuccessStatusCode();
        var animals = await animalsResponse.Content.ReadFromJsonAsync<List<AnimalListItemDto>>();

        Assert.NotNull(animals);
        var calf = Assert.Single(animals!, a => a.FarmTag == "TERNERA-01");
        Assert.Equal("Female", calf.Gender);

        var calfDetailResponse = await _client.GetAsync($"/api/v1/animals/{calf.Id}");
        calfDetailResponse.EnsureSuccessStatusCode();
        var calfDetail = await calfDetailResponse.Content.ReadFromJsonAsync<AnimalDetailDto>();

        Assert.NotNull(calfDetail);
        Assert.Equal(damId, calfDetail!.MotherId);
        Assert.Equal(speciesId, calfDetail.SpeciesId);
        Assert.Equal(birthing!.Id, calfDetail.BirthingId);
    }

    [Fact]
    public async Task RecordBirthing_WithOffspringButNoCategory_StillCreatesAnimal()
    {
        // The admin panel's birthing form doesn't have a category picker — it only
        // asks for the calf's farm tag and sex. CategoryId must be optional end to
        // end or every real submission from the panel fails.
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Bovino NoCat", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate = new DateOnly(2026, 8, 1),
            difficulty = "Normal",
            bornAlive = 1,
            offspring = new[]
            {
                new { childId = Guid.NewGuid(), farmTag = "TERNERA-NOCAT", sex = "F" }
            }
        });

        Assert.True(birthingResponse.IsSuccessStatusCode, await birthingResponse.Content.ReadAsStringAsync());

        var animalsResponse = await _client.GetAsync("/api/v1/animals");
        animalsResponse.EnsureSuccessStatusCode();
        var animals = await animalsResponse.Content.ReadFromJsonAsync<List<AnimalListItemDto>>();

        Assert.Contains(animals!, a => a.FarmTag == "TERNERA-NOCAT");
    }

    /// <summary>
    /// Bug B1 from the Fase 3.5 retrospective: birth weight was collected on the
    /// field-app but discarded between the outbox and the database. The server's
    /// `RegisterOffspringRequest` had no field for it, so every "weighed at birth"
    /// reading was lost — including the gilt-selection metric the client explicitly
    /// asked for. The round-trip below pins the fix.
    /// </summary>
    [Fact]
    public async Task RecordBirthing_WithBirthWeight_RoundTripsToAnimalDetail()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Porcino Peso", gestationDays = 114 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        const decimal weighedAtBirth = 1.45m;
        var birthingResponse = await _client.PostAsJsonAsync("/api/v1/breeding/birthings", new
        {
            damId,
            birthDate = new DateOnly(2026, 8, 1),
            difficulty = "Normal",
            bornAlive = 1,
            offspring = new[]
            {
                new
                {
                    childId = Guid.NewGuid(),
                    farmTag = "LECHON-PESADO-01",
                    sex = "F",
                    birthWeightKg = weighedAtBirth,
                }
            }
        });
        birthingResponse.EnsureSuccessStatusCode();

        var animalsResponse = await _client.GetAsync("/api/v1/animals");
        animalsResponse.EnsureSuccessStatusCode();
        var animals = await animalsResponse.Content.ReadFromJsonAsync<List<AnimalListItemDto>>();

        var calf = animals!.Single(a => a.FarmTag == "LECHON-PESADO-01");

        var calfDetailResponse = await _client.GetAsync($"/api/v1/animals/{calf.Id}");
        calfDetailResponse.EnsureSuccessStatusCode();
        var calfDetail = await calfDetailResponse.Content.ReadFromJsonAsync<AnimalDetailDto>();

        Assert.Equal(weighedAtBirth, calfDetail!.BirthWeightKg);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record BirthingDto(Guid Id);
    private sealed record AnimalListItemDto(Guid Id, string? FarmTag, string Gender);
    private sealed record AnimalDetailDto(Guid Id, Guid SpeciesId, Guid? MotherId, Guid? BirthingId, decimal? BirthWeightKg = null);
}
