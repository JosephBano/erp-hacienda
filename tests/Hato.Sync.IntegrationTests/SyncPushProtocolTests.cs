using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// The mandatory push scenarios of PLAN-FASE-3-4 §2.2. This is the file the plan says to
/// write before looking at the endpoint signature, because push is where farm records get
/// duplicated or quietly dropped.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushProtocolTests(SyncApiFactory factory)
{
    /// <summary>
    /// Art. 3: the identity is sovereign and generatable on the phone. Without this, an
    /// animal registered in the field gets a server-side id, and every event the employee
    /// recorded against it while still offline points at an id that exists nowhere.
    /// </summary>
    [Fact]
    public async Task Push_CreateAnimalWithClientGeneratedId_KeepsThatId()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-clientid");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();

        var result = await context.PushAsync("createAnimal", new { id = animalId, speciesId, sex = "Female" });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        Assert.Equal(animalId.ToString(), result.GetProperty("resultRef").GetString());
    }

    /// <summary>
    /// The employee registers an animal and weighs it in the same offline session. Both
    /// operations travel in one batch and the event refers to the id the phone chose.
    /// </summary>
    [Fact]
    public async Task Push_AnimalAndItsEventInOneBatch_BothLandLinked()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-parentchild");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();

        var batch = await context.PushBatchAsync("device-field", new object[]
        {
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = animalId, speciesId, sex = "Female" },
            },
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "recordAnimalEvent",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new
                {
                    animalId,
                    eventType = "Weighing",
                    occurredAt = DateTimeOffset.UtcNow,
                    recordedBy = "empleado-campo",
                    payloadJson = "{\"weightKg\":210}",
                },
            },
        });

        var results = batch.GetProperty("results").EnumerateArray().ToList();
        Assert.All(results, r => Assert.Equal("Accepted", r.GetProperty("status").GetString()));
    }

    /// <summary>
    /// Reverse order. The plan allows either resolving or queueing, but never losing: the
    /// operation must come back rejected *and* be recorded, so the problems tray can show
    /// it instead of it vanishing.
    /// </summary>
    [Fact]
    public async Task Push_EventBeforeItsAnimal_IsRejectedButRecorded()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-outoforder");
        var orphanAnimalId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        var result = await context.PushAsync(
            "recordAnimalEvent",
            new
            {
                animalId = orphanAnimalId,
                eventType = "Weighing",
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "empleado-campo",
                payloadJson = "{\"weightKg\":180}",
            },
            operationId);

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("errorDetails").GetString()));

        var recorded = await context.GetSyncOperationsAsync("Rejected");
        Assert.Contains(recorded, o => o.GetProperty("clientOperationId").GetString() == operationId.ToString());
    }

    [Fact]
    public async Task Push_SameOperationThreeTimes_ProducesOneRecordAndOneResult()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-triple");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var payload = new { id = animalId, speciesId, sex = "Male" };

        var first = await context.PushAsync("createAnimal", payload, operationId);
        var second = await context.PushAsync("createAnimal", payload, operationId);
        var third = await context.PushAsync("createAnimal", payload, operationId);

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Duplicate", second.GetProperty("status").GetString());
        Assert.Equal("Duplicate", third.GetProperty("status").GetString());

        var reference = first.GetProperty("resultRef").GetString();
        Assert.Equal(reference, second.GetProperty("resultRef").GetString());
        Assert.Equal(reference, third.GetProperty("resultRef").GetString());

        var animals = await context.CollectAsync("animals");
        Assert.Equal(1, animals.Count(row => row.GetProperty("id").GetGuid() == animalId));
    }

    /// <summary>
    /// A batch with one bad apple must not cost the good ones, and replaying the whole
    /// batch afterwards must not double anything.
    /// </summary>
    [Fact]
    public async Task Push_BatchWithOneInvalidOperation_KeepsTheValidOnesAndReplaysCleanly()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-partial");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var operations = new List<object>();
        var validIds = new List<Guid>();

        for (var i = 0; i < 10; i++)
        {
            var isTheBadOne = i == 3;
            var animalId = Guid.NewGuid();
            if (!isTheBadOne)
            {
                validIds.Add(animalId);
            }

            operations.Add(new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new
                {
                    id = animalId,
                    speciesId = isTheBadOne ? Guid.Empty : speciesId,
                    sex = "Female",
                },
            });
        }

        var first = await context.PushBatchAsync("device-field", operations);
        var firstResults = first.GetProperty("results").EnumerateArray().ToList();

        Assert.Equal(9, firstResults.Count(r => r.GetProperty("status").GetString() == "Accepted"));
        Assert.Equal(1, firstResults.Count(r => r.GetProperty("status").GetString() == "Rejected"));

        // The device retries the entire batch, exactly as it would after a lost response.
        var second = await context.PushBatchAsync("device-field", operations);
        var secondResults = second.GetProperty("results").EnumerateArray().ToList();
        Assert.All(secondResults, r => Assert.Equal("Duplicate", r.GetProperty("status").GetString()));

        var present = await context.CollectIdsAsync("animals");

        foreach (var id in validIds)
        {
            Assert.Contains(id, present);
        }
    }

    [Fact]
    public async Task Push_FullSizeBatch_IsProcessedCompletely()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-bigbatch");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var operations = Enumerable.Range(0, 500).Select(_ => new
        {
            clientOperationId = Guid.NewGuid(),
            operationType = "createAnimal",
            occurredAt = DateTimeOffset.UtcNow,
            payload = new { id = Guid.NewGuid(), speciesId, sex = "Female" },
        }).ToList();

        var batch = await context.PushBatchAsync("device-field", operations);

        Assert.Equal(500, batch.GetProperty("processedCount").GetInt32());
        Assert.All(
            batch.GetProperty("results").EnumerateArray(),
            r => Assert.Equal("Accepted", r.GetProperty("status").GetString()));
    }

    /// <summary>
    /// An oversized batch is refused as a whole, before anything is written: a partially
    /// applied giant batch is far harder for a device to reconcile than a clean refusal.
    /// </summary>
    [Fact]
    public async Task Push_BatchOverTheLimit_IsRefusedWithoutWritingAnything()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-toobig");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var animalIds = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList();
        var operations = animalIds.Select(id => new
        {
            clientOperationId = Guid.NewGuid(),
            operationType = "createAnimal",
            occurredAt = DateTimeOffset.UtcNow,
            payload = new { id, speciesId, sex = "Female" },
        }).ToList();

        var response = await context.Client.PostAsJsonAsync(
            "/api/v1/sync/push", new { deviceId = "device-field", operations });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var present = await context.CollectIdsAsync("animals");

        Assert.DoesNotContain(animalIds[0], present);
    }

    /// <summary>
    /// Scenario 6: a phone whose clock is two days ahead. The server orders by its own
    /// clock, but the moment the employee declared is kept verbatim as reported data.
    /// </summary>
    [Fact]
    public async Task Push_FromADeviceWithASkewedClock_PreservesTheDeclaredMoment()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-clockskew");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();

        var declared = DateTimeOffset.UtcNow.AddDays(2);
        var operationId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        await context.PushAsync(
            "createAnimal",
            new { id = animalId, speciesId, sex = "Female" },
            operationId,
            occurredAt: declared);

        var operations = await context.GetSyncOperationsAsync();
        var record = operations.First(o => o.GetProperty("clientOperationId").GetString() == operationId.ToString());

        Assert.Equal(declared.ToUnixTimeSeconds(), record.GetProperty("occurredAt").GetDateTimeOffset().ToUnixTimeSeconds());

        // The server's own ordering clock is unaffected by the device's.
        var receivedAt = record.GetProperty("receivedAt").GetDateTimeOffset();
        Assert.InRange(receivedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
