using System.Net;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Scenario 5 of docs/spec/plan-0001-fase-3/spec.md sec.2.2: a logical delete reaches the client and disappears
/// from its local base. Until <c>Animal.Delete()</c> existed, no domain operation ever set
/// <c>deleted_at</c> anywhere in the system, so this path was entirely untested — the
/// tombstone plumbing in the pull query had never actually carried a real tombstone.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullTombstoneTests(SyncApiFactory factory)
{
    [Fact]
    public async Task DeletedAnimal_ArrivesAtThePullAsATombstone()
    {
        var context = await SyncTestContext.CreateAsync(factory, "tombstone-basic");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var deleteResponse = await context.Client.DeleteAsync($"/api/v1/animals/{animalId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var animal = await context.FindAsync("animals", animalId);

        Assert.True(animal.GetProperty("isDeleted").GetBoolean());
    }

    /// <summary>
    /// The tombstone has to survive being paged past: a client whose cursor already moved
    /// on must still see the delete on its next pull, not lose it because it arrived a
    /// batch late.
    /// </summary>
    [Fact]
    public async Task DeletedAnimal_StillAppearsAfterTheCursorHasAdvancedPastIt()
    {
        var context = await SyncTestContext.CreateAsync(factory, "tombstone-cursor");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var beforeDelete = await context.PullAsync(collections: "animals", batchSize: 1000);
        var cursorBeforeDelete = beforeDelete.GetProperty("cursor").GetString();

        (await context.Client.DeleteAsync($"/api/v1/animals/{animalId}")).EnsureSuccessStatusCode();

        var afterDelete = await context.PullAsync(since: cursorBeforeDelete, collections: "animals");

        var row = afterDelete.GetProperty("collections").GetProperty("animals").EnumerateArray()
            .Single(r => r.GetProperty("id").GetGuid() == animalId);

        Assert.True(row.GetProperty("isDeleted").GetBoolean());
    }
}
