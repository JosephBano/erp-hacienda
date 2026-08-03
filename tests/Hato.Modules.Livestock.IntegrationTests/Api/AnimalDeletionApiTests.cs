using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// Undoing a mis-registered animal (a double-tap in the field, the wrong species picked).
/// This is the only case in the module where a row is meant to disappear from ordinary
/// listings while still existing as a tombstone for the sync protocol (ADR-0008) — so it
/// is tested from both angles: gone from the regular API, present as `isDeleted` for sync.
/// </summary>
public class AnimalDeletionApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Especie-{Guid.NewGuid():N}" });
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

    [Fact]
    public async Task DeleteAnimal_ThatWasJustRegistered_Succeeds()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var response = await _client.DeleteAsync($"/api/v1/animals/{animalId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeletedAnimal_DisappearsFromTheRegularListing()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        (await _client.DeleteAsync($"/api/v1/animals/{animalId}")).EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync($"/api/v1/animals/{animalId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/v1/animals");
        listResponse.EnsureSuccessStatusCode();
        var animals = await listResponse.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.DoesNotContain(animals!, a => a.GetProperty("id").GetGuid() == animalId);
    }

    [Fact]
    public async Task DeleteAnimal_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/v1/animals/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnimal_Twice_ReturnsNotFoundTheSecondTime()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        (await _client.DeleteAsync($"/api/v1/animals/{animalId}")).EnsureSuccessStatusCode();

        var second = await _client.DeleteAsync($"/api/v1/animals/{animalId}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>
    /// Art. 1: an animal with real recorded history is not "a mistake" any more — deleting
    /// it would make its events point at a hidden animal. The safety valve is a new
    /// correcting event, never removing the one the events depend on.
    /// </summary>
    [Fact]
    public async Task DeleteAnimal_WithRecordedEvents_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "test-runner",
            payloadJson = "{\"weightKg\":300}",
        });
        eventResponse.EnsureSuccessStatusCode();

        var response = await _client.DeleteAsync($"/api/v1/animals/{animalId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillThere = await _client.GetAsync($"/api/v1/animals/{animalId}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
