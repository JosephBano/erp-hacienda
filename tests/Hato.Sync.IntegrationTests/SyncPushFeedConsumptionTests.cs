using Hato.Modules.Inventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.7 task 5: "consumo de alimento del lote en sacos"
/// pushed offline. This is the exact defect class the task calls out — `PushSyncCommands`
/// has no <c>UnmappedMemberHandling.Disallow</c>, so a field the client names differently
/// from <see cref="Hato.Modules.Inventory.Application.Consumptions.RecordGroupFeedConsumptionCommand"/>
/// is dropped in silence and the server still answers <c>Accepted</c> — the false-positive
/// close that hit Fase 3. These tests push the exact payload shape
/// `clients/field-app/src/services/feedConsumptionService.ts` enqueues and assert the
/// resulting row, not just the HTTP status, so a renamed field fails loudly here instead of
/// in the field.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushFeedConsumptionTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Push_RecordFeedConsumption_CreatesGroupFeedConsumptionWithExactFields()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-feedconsumption");
        var groupId = await context.CreateGroupAsync("Engorde", trackingMode: "Headcount");
        var itemId = await context.CreateInventoryItemAsync(category: "Feed", unit: "kg");
        // FIFO deduction (feature/inventory-consumption-history B.2) requires stock
        // to exist on the item — otherwise the consumption is rejected with
        // "stock insuficiente". Set up a batch the consumption can drain.
        await context.CreateInventoryBatchAsync(itemId, "L-TEST", 200m);

        var result = await context.PushAsync("recordFeedConsumption", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 120m,
            consumedAt = "2026-08-09",
            recordedBy = "empleado-campo",
            unit = "kg",
            notes = "Ración de la mañana",
        });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var resultRef = result.GetProperty("resultRef").GetString();
        Assert.False(string.IsNullOrWhiteSpace(resultRef));
        var consumptionId = Guid.Parse(resultRef!);

        // The claim that matters: every field the client sent actually landed on the row.
        // A silently-dropped field (e.g. a payload key renamed on one side only) would
        // still return Accepted here but leave GroupId/InventoryItemId/Quantity at their
        // defaults — this is exactly the failure mode the task calls out.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IInventoryDbContext>();
        var consumption = await dbContext.GroupFeedConsumptions
            .FirstOrDefaultAsync(c => c.Id == consumptionId);

        Assert.NotNull(consumption);
        Assert.Equal(groupId, consumption!.GroupId);
        Assert.Equal(itemId, consumption.InventoryItemId);
        Assert.Equal(120m, consumption.QuantityRecorded);
        Assert.Equal(120m, consumption.QuantityInBaseUnit);
        Assert.Equal("kg", consumption.UnitRecorded);
        Assert.Equal(new DateOnly(2026, 8, 9), consumption.ConsumedAt);
        Assert.Equal("Ración de la mañana", consumption.Notes);
    }

    /// <summary>
    /// Same "never duplicate" guarantee every push operation gets from the generic claim
    /// step (docs/spec/plan-0001-fase-3/spec.md sec.2.2), exercised here because feed consumption decrements a
    /// batch — a duplicate would double-deduct stock, not just double a row.
    /// </summary>
    [Fact]
    public async Task Push_SameFeedConsumptionTwice_IsAcceptedOnceThenDuplicate()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-feedconsumption-dedupe");
        var groupId = await context.CreateGroupAsync("Engorde", trackingMode: "Headcount");
        var itemId = await context.CreateInventoryItemAsync(category: "Feed", unit: "kg");
        // See CreateGroupFeedConsumptionWithExactFields above — B.2 needs stock.
        await context.CreateInventoryBatchAsync(itemId, "L-TEST", 50m);
        var operationId = Guid.NewGuid();

        var payload = new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 3m,
            consumedAt = "2026-08-09",
            recordedBy = "empleado-campo",
        };

        var first = await context.PushAsync("recordFeedConsumption", payload, operationId);
        var second = await context.PushAsync("recordFeedConsumption", payload, operationId);

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Duplicate", second.GetProperty("status").GetString());
        Assert.Equal(
            first.GetProperty("resultRef").GetString(),
            second.GetProperty("resultRef").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IInventoryDbContext>();
        var count = await dbContext.GroupFeedConsumptions.CountAsync(c => c.GroupId == groupId);
        Assert.Equal(1, count);
    }
}
