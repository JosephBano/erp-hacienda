using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Livestock.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Feature 0005: animal disposal state arrives at the pull with its timestamp,
/// keeping isDeleted = false (D1, D5: distinct from a logical tombstone).
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullDisposalTests(SyncApiFactory factory)
{
    [Fact]
    public async Task DisposedAnimal_ArrivesAtThePullWithDisposedAtAndNotDeleted()
    {
        var context = await SyncTestContext.CreateAsync(factory, "disposal-pull-basic");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var disposalTime = DateTimeOffset.UtcNow;
        var response = await context.Client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = disposalTime,
            recordedBy = "capataz",
            payloadJson = "{\"disposalType\":\"Death\"}",
        });
        response.EnsureSuccessStatusCode();

        var animal = await context.FindAsync("animals", animalId);

        Assert.False(animal.GetProperty("isDeleted").GetBoolean());
        Assert.True(animal.TryGetProperty("disposedAt", out var disposedAtProp));
        Assert.Equal(JsonValueKind.String, disposedAtProp.ValueKind);
        Assert.NotNull(disposedAtProp.GetString());
    }
}
