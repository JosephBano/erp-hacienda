using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// PLAN-FASE-3-4 §2.2, the closing requirement: "dos dispositivos simulados registran
/// offline, sincronizan y terminan con estado idéntico." Every other test in this suite
/// proves one mechanism in isolation (idempotency, cursor correctness, LWW…) — this one
/// runs the whole protocol the way two actual phones would use it: each device works
/// entirely offline, generating its own ids, and only then pushes and pulls. What matters
/// is the end state, not any single call.
///
/// This drives the real push/pull HTTP protocol against a real Postgres (Testcontainers),
/// which is what makes the convergence claim meaningful; it does not launch the actual
/// React Native app — that is an operational milestone (a phone in the field), not
/// something a test suite can stand in for.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncConvergenceTests(SyncApiFactory factory)
{
    [Fact]
    public async Task TwoDevicesRegisteringIndependentlyOffline_ConvergeToIdenticalState()
    {
        var context = await SyncTestContext.CreateAsync(factory, "convergence-basic");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        // Both devices mint their own identity offline (Art. 3) — this is the case that
        // breaks first if id generation or the push handler ever collide across devices.
        var deviceAAnimalId = Guid.NewGuid();
        var deviceBAnimalId = Guid.NewGuid();

        var deviceAOps = new object[]
        {
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = deviceAAnimalId, speciesId, sex = "Female" },
            },
        };

        var deviceBOps = new object[]
        {
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = deviceBAnimalId, speciesId, sex = "Male" },
            },
        };

        // Neither device has ever synced before, so both push before either pulls —
        // exactly the "two phones came back from the paddock on the same evening" case.
        await context.PushBatchAsync("device-a", deviceAOps);
        await context.PushBatchAsync("device-b", deviceBOps);

        var seenByDeviceA = await context.CollectIdsAsync("animals");
        var seenByDeviceB = await context.CollectIdsAsync("animals");

        Assert.Equal(seenByDeviceA, seenByDeviceB);
        Assert.Contains(deviceAAnimalId, seenByDeviceA);
        Assert.Contains(deviceBAnimalId, seenByDeviceA);
    }

    /// <summary>
    /// One device also records an event against the *other* device's animal — the case
    /// where two phones are working the same herd, not just adding to it independently.
    /// </summary>
    [Fact]
    public async Task TwoDevicesRegisteringAndCrossReferencingEachOthersWork_Converge()
    {
        var context = await SyncTestContext.CreateAsync(factory, "convergence-cross");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var deviceAAnimalId = Guid.NewGuid();

        await context.PushBatchAsync("device-a", new object[]
        {
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = deviceAAnimalId, speciesId, sex = "Female" },
            },
        });

        // Device B pulls first — this is how it learns device A's animal exists at all —
        // then records a weighing against it while still offline itself.
        var devicePull = await context.PullAsync(collections: "animals", batchSize: 1000);
        Assert.Contains(
            devicePull.GetProperty("collections").GetProperty("animals").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == deviceAAnimalId);

        var weighingResult = await context.PushAsync(
            "recordAnimalEvent",
            new
            {
                animalId = deviceAAnimalId,
                eventType = "Weighing",
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "device-b-user",
                payloadJson = "{\"weightKg\":215}",
            },
            deviceId: "device-b");

        Assert.Equal("Accepted", weighingResult.GetProperty("status").GetString());
    }

    /// <summary>
    /// A device that never received the server's response to a batch — connection dropped
    /// right after the request landed — replays the whole thing on its next attempt. The
    /// converged state must be exactly what one successful push would have produced, not
    /// two of everything.
    /// </summary>
    [Fact]
    public async Task DeviceReplayingAnUnacknowledgedBatch_ConvergesWithoutDuplicating()
    {
        var context = await SyncTestContext.CreateAsync(factory, "convergence-replay");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();

        var operations = new object[]
        {
            new
            {
                clientOperationId = Guid.NewGuid(),
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = animalId, speciesId, sex = "Female" },
            },
        };

        // First attempt: the server processed it, but — as far as this device knows — the
        // response never arrived.
        await context.PushBatchAsync("device-a", operations);

        // The device, none the wiser, sends the identical batch again on its next sync.
        var replay = await context.PushBatchAsync("device-a", operations);
        var replayResult = replay.GetProperty("results")[0];
        Assert.Equal("Duplicate", replayResult.GetProperty("status").GetString());

        var animals = await context.CollectAsync("animals");
        Assert.Equal(1, animals.Count(row => row.GetProperty("id").GetGuid() == animalId));
    }
}
