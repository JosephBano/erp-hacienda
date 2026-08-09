using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// BACKLOG "AnimalEvent grupal aún no viaja en el pull (3.5a.1)": 3.5a.1 built the
/// group-subject event mechanism (push, domain, DB CHECK) but no collection ever
/// carried the history back down to the phone. This is the payoff of that mechanism —
/// gated by <c>livestock.animals.read</c> like the rest of the animal-support
/// collections — and it is what 3.5a.7's lot record ("última vacunación, alimento del
/// período") needs to read offline.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullAnimalEventsTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversAnimalSubjectEvent_ToAnyoneWithLivestockRead()
    {
        var context = await SyncTestContext.CreateAsync(factory, "events-animal");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var pushResult = await context.PushAsync("recordAnimalEvent", new
        {
            animalId,
            eventType = "Weighing",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Operario de prueba",
            payloadJson = "{\"kg\":45.5}",
        });
        Assert.Equal("Accepted", pushResult.GetProperty("status").GetString());
        var eventId = pushResult.GetProperty("resultRef").GetString();

        var row = await context.FindAsync("animalEvents", Guid.Parse(eventId!));

        Assert.Equal(animalId, row.GetProperty("animalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("groupId").ValueKind);
        Assert.Equal("Weighing", row.GetProperty("eventType").GetString());
        Assert.False(row.GetProperty("isDeleted").GetBoolean());
    }

    [Fact]
    public async Task Pull_DeliversGroupSubjectEvent_WithoutInventingAnAnimalId()
    {
        var context = await SyncTestContext.CreateAsync(factory, "events-group");
        var groupId = await context.CreateGroupAsync("Lote engorde", trackingMode: "Headcount");

        var pushResult = await context.PushAsync("recordGroupEvent", new
        {
            groupId,
            eventType = "Weighing",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Operario de prueba",
            payloadJson = "{\"sample_count\":10,\"avg_kg\":22.3,\"min_kg\":18,\"max_kg\":27}",
        });
        Assert.Equal("Accepted", pushResult.GetProperty("status").GetString());
        var eventId = pushResult.GetProperty("resultRef").GetString();

        var row = await context.FindAsync("animalEvents", Guid.Parse(eventId!));

        // ADR-0015's whole point: the row honestly declares a group subject, never a
        // fabricated animalId standing in for "some animal in this lot".
        Assert.Equal(JsonValueKind.Null, row.GetProperty("animalId").ValueKind);
        Assert.Equal(groupId, row.GetProperty("groupId").GetGuid());
        Assert.Equal("Weighing", row.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Pull_DeliversGroupDisposalWithAffectedCount()
    {
        var context = await SyncTestContext.CreateAsync(factory, "events-group-disposal");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var groupId = await context.CreateGroupAsync("Lote engorde", trackingMode: "Headcount");

        for (var i = 0; i < 5; i++)
        {
            var animalId = await context.CreateAnimalAsync(speciesId, sex: "Male");
            await context.AddMemberAsync(groupId, animalId);
        }

        var pushResult = await context.PushAsync("recordGroupEvent", new
        {
            groupId,
            eventType = "Disposal",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Operario de prueba",
            payloadJson = "{\"reason\":\"venta parcial\"}",
            affectedCount = 3,
        });
        Assert.Equal("Accepted", pushResult.GetProperty("status").GetString());
        var eventId = pushResult.GetProperty("resultRef").GetString();

        var row = await context.FindAsync("animalEvents", Guid.Parse(eventId!));
        Assert.Equal(3, row.GetProperty("affectedCount").GetInt32());
        Assert.Equal(groupId, row.GetProperty("groupId").GetGuid());
    }

    [Fact]
    public async Task Pull_HidesAnimalEventsFromUsersWithoutLivestockRead()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "events-visible-admin");
        var speciesId = await admin.CreateSpeciesAsync("Bovino");
        var animalId = await admin.CreateAnimalAsync(speciesId);
        await admin.PushAsync("recordAnimalEvent", new
        {
            animalId,
            eventType = "Weighing",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Operario",
            payloadJson = "{\"kg\":40}",
        });

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "events-no-livestock", SystemPermissions.InventoryItemsRead);

        var pull = await limited.PullAsync(collections: "animalEvents", batchSize: 1000);

        Assert.Empty(pull.GetProperty("collections").GetProperty("animalEvents").EnumerateArray());
    }

    /// <summary>
    /// Fase 3 retrospective: the cursor tie-break bug shipped because no test exercised
    /// two rows landing on the exact same instant. animal_events, being an append-only
    /// log that can receive many events in the same request/transaction, is exactly the
    /// table where this collision is most likely in practice.
    /// </summary>
    [Fact]
    public async Task Pull_RowsSharingTheExactCursorTimestamp_AreDeliveredExactlyOnce()
    {
        var context = await SyncTestContext.CreateAsync(factory, "events-cursor-tie");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var start = DateTimeOffset.UtcNow.ToString("O");

        var eventIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var result = await context.PushAsync("recordAnimalEvent", new
            {
                animalId,
                eventType = "Weighing",
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "Operario",
                payloadJson = $"{{\"kg\":{40 + i}}}",
            });
            eventIds.Add(Guid.Parse(result.GetProperty("resultRef").GetString()!));
        }

        var instant = DateTimeOffset.UtcNow;
        await SetCreatedAtAsync(instant, eventIds.ToArray());

        var seen = await DrainAsync(context, batchSize: 2, from: start);

        foreach (var id in eventIds)
        {
            Assert.Equal(1, seen.Count(s => s == id));
        }
    }

    /// <summary>
    /// The exact obligation from the assignment: an incremental pull across pages must
    /// deliver every row exactly once — no duplicates, no gaps — for a mix of animal-
    /// and group-subject events landing in the same window.
    /// </summary>
    [Fact]
    public async Task Pull_IncrementalAcrossManyPages_DeliversEveryAnimalAndGroupEventExactlyOnce()
    {
        var context = await SyncTestContext.CreateAsync(factory, "events-page");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var groupId = await context.CreateGroupAsync("Lote mixto", trackingMode: "Headcount");

        var start = DateTimeOffset.UtcNow.ToString("O");
        var created = new List<Guid>();

        for (var i = 0; i < 6; i++)
        {
            var result = await context.PushAsync("recordAnimalEvent", new
            {
                animalId,
                eventType = "Weighing",
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "Operario",
                payloadJson = $"{{\"kg\":{40 + i}}}",
            });
            created.Add(Guid.Parse(result.GetProperty("resultRef").GetString()!));
        }

        for (var i = 0; i < 6; i++)
        {
            var result = await context.PushAsync("recordGroupEvent", new
            {
                groupId,
                eventType = "Weighing",
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "Operario",
                payloadJson = $"{{\"sample_count\":5,\"avg_kg\":{20 + i}}}",
            });
            created.Add(Guid.Parse(result.GetProperty("resultRef").GetString()!));
        }

        var seen = await DrainAsync(context, batchSize: 4, from: start);

        Assert.Equal(created.Count, seen.Count);
        foreach (var id in created)
        {
            Assert.Equal(1, seen.Count(s => s == id));
        }
    }

    /// <summary>Pulls repeatedly, following the cursor, until the server reports no more data.</summary>
    private static async Task<List<Guid>> DrainAsync(SyncTestContext context, int batchSize, string from)
    {
        var seen = new List<Guid>();
        var cursor = from;

        for (var page = 0; page < 60; page++)
        {
            var pull = await context.PullAsync(since: cursor, collections: "animalEvents", batchSize: batchSize);

            foreach (var row in pull.GetProperty("collections").GetProperty("animalEvents").EnumerateArray())
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

        throw new Xunit.Sdk.XunitException("El pull de 'animalEvents' no convergió: el cursor nunca dejó de reportar hasMore.");
    }

    private async Task SetCreatedAtAsync(DateTimeOffset instant, params Guid[] eventIds)
    {
        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        await using var db = new LivestockDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE livestock.animal_events SET created_at = {0}, updated_at = NULL WHERE id = ANY({1})",
            instant,
            eventIds);
    }
}
