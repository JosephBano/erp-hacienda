using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Api;

/// <summary>
/// BLOQUE D / 3.5a.5: the "bug del saco" — buying in 40-kg sacks and consuming in
/// kg without the numbers talking to each other — is closed by storing the
/// conversion factor at the inventory item level and recording both the
/// operator-typed number and the base-unit number on the consumption row.
///
/// The API surface for conversions is the minimum the field-app needs to
/// download the catalog today: an item's conversions are listed under
/// /api/v1/inventory/items/{id}/unit-conversions, and a new conversion is
/// posted through the same path. Future sync work (PLAN-FASE-3-4 sec.2.2)
/// will fold these into the pull collection; for now the endpoint is
/// reachable for the admin-web / swagger tooling that is already wired up.
/// </summary>
public class UnitConversionApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterFeedItem_WithSackConversion_ThenConsumeInSacks_PersistsBothQuantities()
    {
        // 1. Register a feed item whose base unit is "kg".
        var createItemResponse = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Balanceado-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
            minStock = 0m,
        });
        createItemResponse.EnsureSuccessStatusCode();
        var itemId = (await createItemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 2. Register the conversion sack40kg -> kg with factor 40.
        var createConversionResponse = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/unit-conversions",
            new { fromUnit = "saco40kg", toUnit = "kg", factor = 40m });
        createConversionResponse.EnsureSuccessStatusCode();

        // 3. Record a consumption of 3 sacks on a fictitious group. The endpoint
        // signature takes the operator-typed unit, so the conversion runs in the
        // handler (Art. 10, Art. 8: factor is data, not a hardcoded switch).
        //
        // The group_id is opaque to this test — the conversion happens before any
        // lookup of the group. We use a random Guid and accept that the row will
        // exist with no FK back to a real group: this endpoint is the field-app
        // sync surface, where the group was already pushed.
        var recordResponse = await _client.PostAsJsonAsync("/api/v1/feed-consumptions", new
        {
            groupId = Guid.NewGuid(),
            inventoryItemId = itemId,
            quantity = 3m,
            unit = "saco40kg",
            consumedAt = new DateOnly(2026, 8, 7),
            recordedBy = "capataz",
        });
        recordResponse.EnsureSuccessStatusCode();

        // 4. Pull the consumption row back via DbContext and verify both quantities.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<Hato.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>();
        var consumption = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(
                dbContext.GroupFeedConsumptions,
                c => c.InventoryItemId == itemId);

        Assert.NotNull(consumption);
        Assert.Equal(3m, consumption!.QuantityRecorded);
        Assert.Equal("saco40kg", consumption.UnitRecorded);
        Assert.Equal(120m, consumption.QuantityInBaseUnit);
        Assert.Equal(40m, consumption.AppliedFactor);
    }

    [Fact]
    public async Task RegisterConversion_NegativeFactor_IsRejected()
    {
        var createItemResponse = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Item-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
        });
        createItemResponse.EnsureSuccessStatusCode();
        var itemId = (await createItemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/unit-conversions",
            new { fromUnit = "saco", toUnit = "kg", factor = -1m });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordFeedConsumption_WithoutConversionForUnit_IsRejected()
    {
        var createItemResponse = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Item-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
        });
        createItemResponse.EnsureSuccessStatusCode();
        var itemId = (await createItemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var response = await _client.PostAsJsonAsync("/api/v1/feed-consumptions", new
        {
            groupId = Guid.NewGuid(),
            inventoryItemId = itemId,
            quantity = 3m,
            unit = "saco40kg",
            consumedAt = new DateOnly(2026, 8, 7),
            recordedBy = "capataz",
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}