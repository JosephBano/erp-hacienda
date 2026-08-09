using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// BLOQUE C / C4: the cancel ↔ push race on the same clientOperationId.
///
/// The phone's outbox has two writers that can look at the same row simultaneously:
/// pushOutbox reads pending entries and dispatches them, while cancelPending tries
/// to transition the row from pending to cancelled. The race window is between
/// "read pending" and "mark synced/cancelled". The WatermelonDB transaction in
/// cancelPending is the only thing protecting the local state.
///
/// The server-side counterpart of that race is the same clientOperationId arriving
/// twice — once from the in-flight push, once from a retry triggered by the
/// cancelled-then-recovered state. The server's guarantee (PushSyncCommands.cs
/// ClaimAsync, line 140) is that the unique index on client_operation_id serializes
/// concurrent claims, so any number of pushes with the same id produce exactly one
/// business record. A "phantom record" — the operator sees nothing, the server
/// has a row — would mean the claim step let two inserts slip through; the test
/// below exercises the race hard enough to flush that out.
///
/// cancelPending has no server endpoint (it's a local-only state change in
/// outbox.ts:169), so the test target is the server-side invariant: across many
/// concurrent pushes with the same id, the server must produce exactly one
/// Accepted response and one business record, and the rest must come back
/// Duplicate. If the claim step ever regresses to a "select then insert" without
/// the unique-index backstop, this test will start seeing two Accepted responses
/// and two rows in the animals collection.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncOutboxRaceTests(SyncApiFactory factory)
{
    private const int Iterations = 25;
    private const int ConcurrencyPerIteration = 5;

    [Fact]
    public async Task ConcurrentPushesWithSameClientOperationId_ProduceExactlyOneRecord()
    {
        var context = await SyncTestContext.CreateAsync(factory, "race-cancel-push");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var operationIds = Enumerable.Range(0, Iterations)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var expectedAnimalIds = new ConcurrentBag<Guid>();
        var acceptedPerIteration = new ConcurrentBag<int>();
        var duplicatePerIteration = new ConcurrentBag<int>();
        var otherStatuses = new ConcurrentBag<string>();

        foreach (var operationId in operationIds)
        {
            var animalId = Guid.NewGuid();
            expectedAnimalIds.Add(animalId);

            var payloads = Enumerable.Range(0, ConcurrencyPerIteration).Select(_ => new
            {
                clientOperationId = operationId,
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = new { id = animalId, speciesId, sex = "Female" },
            }).ToArray();

            var responses = await Task.WhenAll(payloads.Select(p =>
                context.Client.PostAsJsonAsync(
                    "/api/v1/sync/push",
                    new { deviceId = "device-a", operations = new[] { p } })));

            var accepted = 0;
            var duplicate = 0;
            foreach (var response in responses)
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                var result = body.GetProperty("results")[0];
                var status = result.GetProperty("status").GetString();
                switch (status)
                {
                    case "Accepted": accepted++; break;
                    case "Duplicate": duplicate++; break;
                    default: otherStatuses.Add(status ?? "<null>"); break;
                }
            }

            acceptedPerIteration.Add(accepted);
            duplicatePerIteration.Add(duplicate);
        }

        Assert.Empty(otherStatuses);
        Assert.All(acceptedPerIteration, c => Assert.Equal(1, c));
        Assert.All(duplicatePerIteration, c => Assert.Equal(ConcurrencyPerIteration - 1, c));

        var present = await context.CollectIdsAsync("animals");
        foreach (var expectedId in expectedAnimalIds)
        {
            Assert.Contains(expectedId, present);
        }
        Assert.Equal(Iterations, expectedAnimalIds.Count(id => present.Contains(id)));
    }
}