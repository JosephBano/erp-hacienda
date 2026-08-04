using System.Net.Http.Json;
using System.Text.Json;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// The species/breed/category catalogs a new animal is registered against. Without a way
/// to list them, no client — the admin panel included — has any way to know what exists
/// to pick from; only creation endpoints existed until now.
/// </summary>
public class CatalogsApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSpecies_ReturnsPreviouslyCreatedSpecies()
    {
        var name = $"Bovino-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name, gestationDays = 283 });
        createResponse.EnsureSuccessStatusCode();
        var speciesId = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var listResponse = await _client.GetAsync("/api/v1/species");
        listResponse.EnsureSuccessStatusCode();
        var species = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();

        Assert.Contains(species!, s =>
            s.GetProperty("id").GetGuid() == speciesId && s.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task GetBreeds_FilteredBySpecies_ExcludesBreedsOfOtherSpecies()
    {
        var speciesA = await CreateSpeciesAsync();
        var speciesB = await CreateSpeciesAsync();

        var breedA = await CreateBreedAsync(speciesA, "Holstein");
        await CreateBreedAsync(speciesB, "Duroc");

        var response = await _client.GetAsync($"/api/v1/breeds?speciesId={speciesA}");
        response.EnsureSuccessStatusCode();
        var breeds = await response.Content.ReadFromJsonAsync<List<JsonElement>>();

        Assert.Single(breeds!);
        Assert.Equal(breedA, breeds![0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetAnimalCategories_FilteredBySpecies_ExcludesCategoriesOfOtherSpecies()
    {
        var speciesA = await CreateSpeciesAsync();
        var speciesB = await CreateSpeciesAsync();

        var categoryA = await CreateCategoryAsync(speciesA, "Vaca lechera");
        await CreateCategoryAsync(speciesB, "Cerdo de engorde");

        var response = await _client.GetAsync($"/api/v1/animal-categories?speciesId={speciesA}");
        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<List<JsonElement>>();

        Assert.Single(categories!);
        Assert.Equal(categoryA, categories![0].GetProperty("id").GetGuid());
    }

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Especie-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateBreedAsync(Guid speciesId, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/breeds", new { speciesId, name = $"{name}-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateCategoryAsync(Guid speciesId, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animal-categories", new { speciesId, name = $"{name}-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private sealed record CreatedId(Guid Id);
}
