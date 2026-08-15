using System.Net.Http.Json;
using Hato.Modules.Inventory.Domain;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Api;

/// <summary>
/// FIFO deduction coverage for the <c>feature/inventory-consumption-history</c>
/// branch (B.2 of the proposal):
///
/// <list type="bullet">
///   <item>When a consumption is recorded without <c>batchId</c>, the handler
///         must pick the oldest batch (by <c>ReceivedAt</c>) that still has
///         stock and deduct from it. FIFO is what an operator expects from
///         "agarrá lo que entró primero" — the same intuition a real warehouse
///         follows.</item>
///   <item>If the total quantity to deduct spans more than one batch, the
///         first batch is drained and the remainder moves to the next one.
///         The <see cref="RecordFeedConsumption_SpansMultipleBatches_DrainsInFifoOrder"/>
///         test pins that behaviour.</item>
///   <item>If there is no batch with stock, the consumption is rejected (the
///         domain rule: deducting from nothing is meaningless).</item>
/// </list>
///
/// Tests for the explicit-batch case (caller passed <c>batchId</c>) are not
/// repeated here — that path was already covered by the consumption feature's
/// own integration suite and is unchanged by this branch.
/// </summary>
public class RecordFeedConsumptionFifoApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordFeedConsumption_WithoutBatchId_DeductsFromOldestBatchWithStock()
    {
        // Two batches, the OLDER one has stock — that is the one the FIFO logic
        // must drain. The newer batch must be untouched.
        var itemId = await CreateFeedItemAsync("Fifo-Deduct-Oldest");
        var oldBatchId = await CreateReceptionAsync(
            itemId, "L-OLD",
            quantity: 20m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.AddDays(-10).ToString("o"));
        var newBatchId = await CreateReceptionAsync(
            itemId, "L-NEW",
            quantity: 50m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.AddDays(-2).ToString("o"));

        var groupId = await CreateAnimalGroupAsync("FIFO-LOTE");
        await RecordConsumptionAsync(itemId, groupId, quantity: 7m, batchId: null);

        var quantities = await ReadBatchQuantitiesAsync(itemId, oldBatchId, newBatchId);

        // FIFO: oldest batch is drained first.
        Assert.Equal(13m, quantities[oldBatchId]);
        Assert.Equal(50m, quantities[newBatchId]); // untouched
    }

    [Fact]
    public async Task RecordFeedConsumption_SpansMultipleBatches_DrainsInFifoOrder()
    {
        // Old batch has only 5 kg; the consumption wants 12 kg. The FIFO logic
        // must drain the old one and continue with the next-oldest that still
        // has stock. The newest batch (after both) must remain untouched.
        var itemId = await CreateFeedItemAsync("Fifo-Deduct-Span");
        var oldBatchId = await CreateReceptionAsync(
            itemId, "L-OLD",
            quantity: 5m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.AddDays(-15).ToString("o"));
        var midBatchId = await CreateReceptionAsync(
            itemId, "L-MID",
            quantity: 20m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.AddDays(-7).ToString("o"));
        var newBatchId = await CreateReceptionAsync(
            itemId, "L-NEW",
            quantity: 100m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.AddDays(-1).ToString("o"));

        var groupId = await CreateAnimalGroupAsync("FIFO-SPAN-LOTE");
        await RecordConsumptionAsync(itemId, groupId, quantity: 12m, batchId: null);

        var quantities = await ReadBatchQuantitiesAsync(itemId, oldBatchId, midBatchId, newBatchId);

        // Old batch drained to 0; middle batch absorbed the remaining 7 kg;
        // new batch untouched.
        Assert.Equal(0m, quantities[oldBatchId]);
        Assert.Equal(13m, quantities[midBatchId]);
        Assert.Equal(100m, quantities[newBatchId]);
    }

    [Fact]
    public async Task RecordFeedConsumption_NotEnoughStockAcrossBatches_ReturnsBadRequest()
    {
        var itemId = await CreateFeedItemAsync("Fifo-NoStock");
        await CreateReceptionAsync(
            itemId, "L-TINY",
            quantity: 1m, unit: "kg",
            receivedAt: DateTimeOffset.UtcNow.ToString("o"));

        var groupId = await CreateAnimalGroupAsync("FIFO-EMPTY-LOTE");
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 3m,
            unit = "kg",
            consumedAt = new DateOnly(2026, 8, 14).ToString("yyyy-MM-dd"),
            recordedBy = "integration-test",
            // batchId intentionally omitted — falls into the FIFO branch.
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- helpers ----

    private async Task<Guid> CreateFeedItemAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            category = ItemCategory.Feed.ToString(),
            unit = "kg",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalGroupAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            trackingMode = "Headcount",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    /// <summary>
    /// Creates a batch via the legacy POST /batches endpoint (ADR-0026 keeps this
    /// path working for technical/manual adjustments). It auto-sets
    /// <c>ReceivedAt = UtcNow</c> on each call, so consecutive invocations produce
    /// batches with distinct timestamps — exactly what the FIFO test needs to
    /// assert draining order. The reception endpoint can't be used here because
    /// its <c>receivedAt</c> cannot precede the item's <c>CreatedAt</c>, and an
    /// item just created has no past we can choose from.
    /// </summary>
    private async Task<Guid> CreateReceptionAsync(
        Guid itemId, string batchNumber, decimal quantity, string unit, string receivedAt)
    {
        // The `receivedAt` parameter is kept for symmetry with the public API but
        // ignored — the legacy endpoint owns the timestamp.
        _ = receivedAt;
        var response = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/batches", new
        {
            batchNumber,
            quantity,
            costPerUnit = 1m,
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"CreateReceptionAsync failed: {(int)response.StatusCode} {response.StatusCode}\nBody: {body}\nRequest: itemId={itemId}, batchNumber={batchNumber}, quantity={quantity}, unit={unit}, receivedAt={receivedAt}");
        }
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task RecordConsumptionAsync(
        Guid itemId, Guid groupId, decimal quantity, Guid? batchId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity,
            unit = "kg",
            consumedAt = new DateOnly(2026, 8, 14).ToString("yyyy-MM-dd"),
            recordedBy = "integration-test",
            batchId,
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Dictionary<Guid, decimal>> ReadBatchQuantitiesAsync(
        Guid itemId, params Guid[] batchIds)
    {
        var batches = await _client.GetFromJsonAsync<List<BatchQty>>(
            $"/api/v1/inventory/items/{itemId}/batches");

        var lookup = batches!.ToDictionary(b => b.Id, b => b.Quantity);
        var result = new Dictionary<Guid, decimal>();
        foreach (var id in batchIds)
        {
            Assert.True(lookup.ContainsKey(id), $"Batch {id} not present in /batches response");
            result[id] = lookup[id];
        }
        return result;
    }

    private sealed record CreatedId(Guid Id);
    private sealed record BatchQty(Guid Id, decimal Quantity);
}