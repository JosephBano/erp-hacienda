using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.Animals;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

public class AnimalIdentityApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterAnimal_AssignIdentifier_AndFetch_RoundTripsThroughPostgres()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var identifierResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/identifiers", new
        {
            type = IdentifierType.FarmTag,
            value = "A-101",
            validFrom = new DateOnly(2026, 1, 1),
        });
        identifierResponse.EnsureSuccessStatusCode();

        // Replaces the tag — exercises the path where AssignIdentifier closes an
        // already-tracked (previously persisted) identifier instead of creating one.
        var replacementResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/identifiers", new
        {
            type = IdentifierType.FarmTag,
            value = "A-102",
            validFrom = new DateOnly(2026, 3, 1),
        });
        replacementResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync($"/api/v1/animals/{animalId}");
        getResponse.EnsureSuccessStatusCode();
        var animal = await getResponse.Content.ReadFromJsonAsync<AnimalDto>();

        Assert.NotNull(animal);
        Assert.Equal(speciesId, animal!.SpeciesId);
        Assert.Equal(2, animal.Identifiers.Count);
        Assert.Contains(animal.Identifiers, i => i.Value == "A-101" && i.ValidTo == new DateOnly(2026, 3, 1));
        Assert.Contains(animal.Identifiers, i => i.Value == "A-102" && i.ValidTo == null);
    }

    [Fact]
    public async Task RegisterAnimal_WithoutSpecies_ReturnsBadRequestProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId = Guid.Empty,
            sex = Sex.Male,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAnimal_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/animals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
