using System.Text.Json;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Cursor semantics (PLAN-FASE-3-4 sec.2.2, scenario 4). A cursor that repeats rows wastes
/// bandwidth; a cursor that skips them loses farm records silently, which is the failure
/// mode this whole suite exists to prevent.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullCursorTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_WithCursorFromPreviousPull_DeliversRowCreatedAfterwards()
    {
        var context = await SyncTestContext.CreateAsync(factory, "cursor-new");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var first = await context.PullAsync();
        var cursor = first.GetProperty("cursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor), "El pull inicial debe devolver un cursor utilizable.");

        var animalId = await context.CreateAnimalAsync(speciesId);

        var second = await context.PullAsync(since: cursor, collections: "animals");

        Assert.Contains(
            second.GetProperty("collections").GetProperty("animals").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == animalId);
    }

    [Fact]
    public async Task Pull_WithCursorFromPreviousPull_DoesNotRedeliverAlreadySyncedRow()
    {
        var context = await SyncTestContext.CreateAsync(factory, "cursor-norepeat");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var first = await context.PullAsync(collections: "animals", batchSize: 1000);
        Assert.Contains(
            first.GetProperty("collections").GetProperty("animals").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == animalId);
        var cursor = first.GetProperty("cursor").GetString();

        var second = await context.PullAsync(since: cursor);

        Assert.DoesNotContain(
            second.GetProperty("collections").GetProperty("animals").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == animalId);
    }

    [Fact]
    public async Task Pull_RowsSharingTheExactCursorTimestamp_AreDeliveredExactlyOnce()
    {
        var context = await SyncTestContext.CreateAsync(factory, "cursor-tie");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var start = DateTimeOffset.UtcNow.ToString("O");

        var a = await context.CreateAnimalAsync(speciesId);
        var b = await context.CreateAnimalAsync(speciesId);
        var c = await context.CreateAnimalAsync(speciesId);

        // Collapse all three onto one instant: this is the "two writes in the same
        // millisecond" boundary that a timestamp-only cursor cannot resolve.
        var instant = DateTimeOffset.UtcNow;
        await SetCreatedAtAsync(instant, a, b, c);

        var seen = await DrainAsync(context, batchSize: 2, from: start);

        Assert.Equal(1, seen.Count(id => id == a));
        Assert.Equal(1, seen.Count(id => id == b));
        Assert.Equal(1, seen.Count(id => id == c));
    }

    [Fact]
    public async Task Pull_PaginatedAcrossManyPages_DeliversEveryRowExactlyOnce()
    {
        var context = await SyncTestContext.CreateAsync(factory, "cursor-page");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var start = DateTimeOffset.UtcNow.ToString("O");

        var created = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            created.Add(await context.CreateAnimalAsync(speciesId));
        }

        var seen = await DrainAsync(context, batchSize: 5, from: start);

        foreach (var id in created)
        {
            Assert.Equal(1, seen.Count(s => s == id));
        }
    }

    /// <summary>
    /// Pulls repeatedly, following the cursor, until the server reports no more data —
    /// exactly what the mobile client does — and returns every animal id it observed.
    /// </summary>
    private static async Task<List<Guid>> DrainAsync(SyncTestContext context, int batchSize, string from)
    {
        var seen = new List<Guid>();
        var cursor = from;

        for (var page = 0; page < 60; page++)
        {
            var pull = await context.PullAsync(since: cursor, collections: "animals", batchSize: batchSize);

            foreach (var row in pull.GetProperty("collections").GetProperty("animals").EnumerateArray())
            {
                seen.Add(row.GetProperty("id").GetGuid());
            }

            var next = pull.GetProperty("cursor").GetString();
            var hasMore = pull.GetProperty("hasMore").GetBoolean();

            if (!hasMore)
            {
                return seen;
            }

            Assert.NotEqual(cursor, next);
            cursor = next;
        }

        throw new Xunit.Sdk.XunitException("El pull no convergió: el cursor nunca dejó de reportar hasMore.");
    }

    private async Task SetCreatedAtAsync(DateTimeOffset instant, params Guid[] animalIds)
    {
        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        await using var db = new LivestockDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE livestock.animals SET created_at = {0}, updated_at = NULL WHERE id = ANY({1})",
            instant,
            animalIds);
    }
}
