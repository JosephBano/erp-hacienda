using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Inventory.Domain;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Api;

/// <summary>
/// Read-side coverage for the consumption-history feature
/// (<c>feature/inventory-consumption-history</c>):
///
/// <list type="bullet">
///   <item><c>GET /api/v1/inventory/items/{itemId}/consumptions</c> — the list
///         the panel needs to render "Consumos registrados" under the item detail.
///         Each row carries the group name (so the user knows which lot got fed),
///         the quantity the operator typed + the unit they typed it in, and the
///         same quantity expressed in the item's base unit.</item>
///   <item>Empty list returns an empty array, not 404 — there is no consumptions
///         resource for "an item that has no consumption yet", only for "an item
///         that does not exist" (which is covered separately).</item>
/// </list>
///
/// The FIFO deduction behaviour for un-batched consumptions lives in
/// <see cref="RecordFeedConsumptionFifoApiTests"/>.
/// </summary>
public class GetInventoryConsumptionsApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetInventoryConsumptions_NoConsumptionsYet_ReturnsEmptyArray()
    {
        var itemId = await CreateItemAsync("Empty-Cons", ItemCategory.Feed, "kg");

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemId}/consumptions");

        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<ConsumptionRowDto>>();

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task GetInventoryConsumptions_WithRecordedConsumptions_ReturnsAllOfThem()
    {
        var itemId = await CreateItemAsync("Cons-History", ItemCategory.Feed, "kg");
        // FIFO deduction (B.2) requires stock to exist on the item. Two batches
        // give the test room for both consumptions to actually land.
        await CreateBatchAsync(itemId, "L-1", 100m);
        var groupA = await CreateAnimalGroupAsync("LOTE-A");
        var groupB = await CreateAnimalGroupAsync("LOTE-B");

        // Two consumptions on the same item, different lots, different days.
        await RecordConsumptionAsync(itemId, groupA, "5", "kg", new DateOnly(2026, 8, 10));
        await RecordConsumptionAsync(itemId, groupB, "8", "kg", new DateOnly(2026, 8, 12));

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemId}/consumptions");

        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<ConsumptionRowDto>>();

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);

        // Most-recent-first ordering (matches the panel's "Lo que registré hoy"
        // mental model — docs/planes/fase-3-5/spec.md sec.2.3).
        Assert.Equal(new DateOnly(2026, 8, 12), rows[0].ConsumedAt);
        Assert.Equal(new DateOnly(2026, 8, 10), rows[1].ConsumedAt);

        var onDay12 = rows[0];
        Assert.Equal(groupB, onDay12.GroupId);
        Assert.StartsWith("LOTE-B", onDay12.GroupName);
        Assert.Equal(itemId, onDay12.InventoryItemId);
        Assert.StartsWith("Cons-History", onDay12.InventoryItemName);
        Assert.Equal(8m, onDay12.QuantityRecorded);
        Assert.Equal("kg", onDay12.UnitRecorded);
        Assert.Equal(8m, onDay12.QuantityInBaseUnit);
        Assert.Equal(1m, onDay12.AppliedFactor);
    }

    [Fact]
    public async Task GetInventoryConsumptions_OnlyReturnsRowsForTheRequestedItem()
    {
        var itemA = await CreateItemAsync("Iso-A", ItemCategory.Feed, "kg");
        var itemB = await CreateItemAsync("Iso-B", ItemCategory.Feed, "kg");
        // Each item needs its own batch — FIFO deduction (B.2) requires stock.
        await CreateBatchAsync(itemA, "L-A", 100m);
        await CreateBatchAsync(itemB, "L-B", 100m);
        var group = await CreateAnimalGroupAsync("LOTE-ISO");

        await RecordConsumptionAsync(itemA, group, "3", "kg", new DateOnly(2026, 8, 10));
        await RecordConsumptionAsync(itemB, group, "7", "kg", new DateOnly(2026, 8, 11));

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemA}/consumptions");

        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<ConsumptionRowDto>>();

        Assert.NotNull(rows);
        Assert.Single(rows!);
        Assert.Equal(3m, rows[0].QuantityRecorded);
        Assert.StartsWith("Iso-A", rows[0].InventoryItemName);
    }

    [Fact]
    public async Task GetInventoryConsumptions_UnknownItem_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/inventory/items/{Guid.NewGuid()}/consumptions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- helpers ----

    private async Task<Guid> CreateItemAsync(string name, ItemCategory category, string unit)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            category = category.ToString(),
            unit,
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

    private async Task CreateBatchAsync(Guid itemId, string batchNumber, decimal quantity)
    {
        // Legacy /batches path: ReceivedAt = UtcNow, no operator-declared metadata.
        // Fine for setting up stock the consumption can drain.
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/batches",
            new { batchNumber, quantity, costPerUnit = 1m });
        response.EnsureSuccessStatusCode();
    }

    private async Task RecordConsumptionAsync(
        Guid itemId, Guid groupId, string quantity, string unit, DateOnly consumedAt)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity,
            unit,
            consumedAt = consumedAt.ToString("yyyy-MM-dd"),
            recordedBy = "integration-test",
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record CreatedId(Guid Id);

    /// <summary>
    /// Wire-format DTO. Mirrors the production contract
    /// (<c>InventoryConsumptionListItemDto</c> in
    /// <c>Hato.Modules.Inventory.Contracts</c>); keeping it local to the test
    /// means the test breaks loudly if the contract drifts in a way that
    /// matters to the panel.
    /// </summary>
    private sealed record ConsumptionRowDto(
        Guid Id,
        Guid GroupId,
        string GroupName,
        Guid InventoryItemId,
        string InventoryItemName,
        decimal QuantityRecorded,
        string UnitRecorded,
        decimal QuantityInBaseUnit,
        decimal AppliedFactor,
        Guid? BatchId,
        DateOnly ConsumedAt,
        string RecordedByLabel,
        string? Notes);
}