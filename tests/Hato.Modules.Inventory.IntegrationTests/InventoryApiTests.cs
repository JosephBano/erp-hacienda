using System.Net.Http.Json;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Domain;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests;

public class InventoryApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateItem_AddBatch_AndRecordFeedConsumption_RoundTripsThroughPostgres()
    {
        // 1. Create Inventory Item
        var itemResponse = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = "Balanceado Ordeño 18%",
            category = ItemCategory.Feed,
            unit = "kg",
            minStock = 200m,
            description = "Alimento balanceado concentrado para vacas en ordeño"
        });
        itemResponse.EnsureSuccessStatusCode();
        var itemId = (await itemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 2. Add Batch to Item
        var batchResponse = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/batches", new
        {
            batchNumber = "LOT-2026-08-A",
            quantity = 500.0m,
            costPerUnit = 0.42m,
            expirationDate = new DateOnly(2026, 12, 31)
        });
        batchResponse.EnsureSuccessStatusCode();
        var batchId = (await batchResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 3. Record Feed Consumption for a Group
        var groupId = Guid.NewGuid();
        var consumptionResponse = await _client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            batchId,
            quantity = 150.0m,
            consumedAt = new DateOnly(2026, 8, 1),
            recordedBy = "mayordomo 1",
            notes = "Alimentación matutina grupo lote 1"
        });
        consumptionResponse.EnsureSuccessStatusCode();

        // 4. Verify Updated Stock
        var getItemsResponse = await _client.GetAsync("/api/v1/inventory/items?category=Feed");
        getItemsResponse.EnsureSuccessStatusCode();
        var items = await getItemsResponse.Content.ReadFromJsonAsync<List<InventoryItemDto>>();

        Assert.NotNull(items);
        var item = items!.First(i => i.Id == itemId);
        Assert.Equal(350.0m, item.TotalStock); // 500 - 150
    }

    private sealed record CreatedId(Guid Id);
}
