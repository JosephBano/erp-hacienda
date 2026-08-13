using Hato.Modules.Inventory.Domain;
using Hato.Modules.Inventory.Domain.Events;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Inventory.UnitTests.Inventory;

/// <summary>
/// ADR-0026 Decisión 3: <see cref="InventoryItem.RecordReception"/> is the canonical
/// entry-point for stock entering the finca. These tests pin the invariants the
/// handler (and any future caller) relies on: positive quantity, non-negative cost,
/// non-future receivedAt, receivedAt not before the item was created, non-empty
/// batch number, and the emitted event carries the operator-declared metadata.
/// </summary>
public class RecordReceptionTests
{
    private static InventoryItem NewFeedItem() =>
        InventoryItem.Create("Balanceado Ordeño 18%", ItemCategory.Feed, "kg", 200);

    [Fact]
    public void RecordReception_WithValidData_CreatesBatchAndRaisesEvent()
    {
        var item = NewFeedItem();
        var receivedAt = item.CreatedAt;

        var batch = item.RecordReception(
            batchNumber: "LOT-2026-08-A",
            quantityInBaseUnit: 500m,
            unitRecorded: "kg",
            appliedFactor: 1m,
            costPerUnit: 0.42m,
            expirationDate: new DateOnly(2026, 12, 31),
            receivedAt: receivedAt,
            supplierLabel: "Agropecuaria XYZ S.A.",
            invoiceReference: "F-001-002345",
            notes: "Llegó en camión propio",
            recordedById: Guid.NewGuid(),
            recordedByLabel: "mayordomo 1");

        Assert.Single(item.Batches);
        Assert.Equal(500m, batch.Quantity);
        Assert.Equal(0.42m, batch.CostPerUnit);
        Assert.Equal("Agropecuaria XYZ S.A.", batch.SupplierLabel);
        Assert.Equal("F-001-002345", batch.InvoiceReference);
        Assert.Equal("mayordomo 1", batch.RecordedByLabel);
        Assert.Equal(receivedAt, batch.ReceivedAt);

        var evt = Assert.Single(item.DomainEvents.OfType<InventoryReceptionRecorded>());
        Assert.Equal(batch.Id, evt.BatchId);
        Assert.Equal(item.Id, evt.ItemId);
        Assert.Equal(500m, evt.QuantityInBaseUnit);
        Assert.Equal("kg", evt.UnitRecorded);
        Assert.Equal(1m, evt.AppliedFactor);
        Assert.Equal(receivedAt, evt.ReceivedAt);
        Assert.Equal("Agropecuaria XYZ S.A.", evt.SupplierLabel);
        Assert.Equal("F-001-002345", evt.InvoiceReference);
        Assert.Equal("mayordomo 1", evt.RecordedBy);
    }

    [Fact]
    public void RecordReception_ZeroOrNegativeQuantity_Throws()
    {
        var item = NewFeedItem();
        var receivedAt = item.CreatedAt;

        var ex0 = Assert.Throws<DomainException>(() => item.RecordReception(
            "LOT-1", 0m, "kg", 1m, 0.5m, null, receivedAt,
            null, null, null, null, null));
        Assert.True(
            ex0.Message.Contains("cantidad", StringComparison.OrdinalIgnoreCase)
            || ex0.Message.Contains("quantity", StringComparison.OrdinalIgnoreCase),
            $"Expected message to mention 'cantidad'/'quantity'. Got: {ex0.Message}");

        Assert.Throws<DomainException>(() => item.RecordReception(
            "LOT-2", -10m, "kg", 1m, 0.5m, null, receivedAt,
            null, null, null, null, null));
    }

    [Fact]
    public void RecordReception_NegativeCost_Throws()
    {
        var item = NewFeedItem();

        Assert.Throws<DomainException>(() => item.RecordReception(
            "LOT-1", 10m, "kg", 1m, -0.5m, null, item.CreatedAt,
            null, null, null, null, null));
    }

    [Fact]
    public void RecordReception_FutureReceivedAt_Throws()
    {
        var item = NewFeedItem();
        var future = DateTimeOffset.UtcNow.AddMinutes(5);

        var ex = Assert.Throws<DomainException>(() => item.RecordReception(
            "LOT-1", 10m, "kg", 1m, 0.5m, null, future,
            null, null, null, null, null));

        Assert.True(
            ex.Message.Contains("futuro", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("future", StringComparison.OrdinalIgnoreCase),
            $"Expected message to mention 'futuro'/'future'. Got: {ex.Message}");
    }

    [Fact]
    public void RecordReception_ReceivedAtBeforeItemCreatedAt_Throws()
    {
        var item = NewFeedItem();
        // InventoryItem.Create stamped CreatedAt = UtcNow at construction time, so anything
        // earlier than that is impossible by construction.
        var before = item.CreatedAt.AddSeconds(-1);

        var ex = Assert.Throws<DomainException>(() => item.RecordReception(
            "LOT-1", 10m, "kg", 1m, 0.5m, null, before,
            null, null, null, null, null));

        Assert.True(
            ex.Message.Contains("anterior", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("before", StringComparison.OrdinalIgnoreCase),
            $"Expected message to mention 'anterior'/'before'. Got: {ex.Message}");
    }

    [Fact]
    public void RecordReception_EmptyBatchNumber_Throws()
    {
        var item = NewFeedItem();

        Assert.Throws<DomainException>(() => item.RecordReception(
            "   ", 10m, "kg", 1m, 0.5m, null, item.CreatedAt,
            null, null, null, null, null));
    }

    [Fact]
    public void RecordReception_BatchNumberExceedsMaxLength_Throws()
    {
        var item = NewFeedItem();

        Assert.Throws<DomainException>(() => item.RecordReception(
            new string('X', 51), 10m, "kg", 1m, 0.5m, null, item.CreatedAt,
            null, null, null, null, null));
    }

    [Fact]
    public void RecordReception_NullableAuthorFields_AreAcceptedAsNull()
    {
        var item = NewFeedItem();

        var batch = item.RecordReception(
            "LOT-1", 10m, "kg", 1m, 0.5m, null, item.CreatedAt,
            supplierLabel: null, invoiceReference: null, notes: null,
            recordedById: null, recordedByLabel: null);

        Assert.Null(batch.SupplierLabel);
        Assert.Null(batch.InvoiceReference);
        Assert.Null(batch.Notes);
        Assert.Null(batch.RecordedById);
        Assert.Null(batch.RecordedByLabel);

        var evt = Assert.Single(item.DomainEvents.OfType<InventoryReceptionRecorded>());
        Assert.Null(evt.SupplierLabel);
        Assert.Null(evt.InvoiceReference);
        Assert.Null(evt.RecordedBy);
    }

    [Fact]
    public void RecordReception_EmittedEvent_OccurredAtDefaultsToUtcNow()
    {
        var item = NewFeedItem();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        item.RecordReception(
            "LOT-1", 10m, "kg", 1m, 0.5m, null, item.CreatedAt,
            null, null, null, null, null);

        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        var evt = Assert.Single(item.DomainEvents.OfType<InventoryReceptionRecorded>());

        Assert.InRange(evt.OccurredAt, before, after);
    }

    [Fact]
    public void RecordReception_TrimsStringFieldsOnBatch()
    {
        var item = NewFeedItem();

        var batch = item.RecordReception(
            "  LOT-2026-08-A  ", 10m, "kg", 1m, 0.5m, null,
            item.CreatedAt,
            supplierLabel: "  Agro XYZ  ",
            invoiceReference: "  F-001  ",
            notes: "  Llegó tarde  ",
            recordedById: null,
            recordedByLabel: "  mayordomo 1  ");

        Assert.Equal("LOT-2026-08-A", batch.BatchNumber);
        Assert.Equal("Agro XYZ", batch.SupplierLabel);
        Assert.Equal("F-001", batch.InvoiceReference);
        Assert.Equal("Llegó tarde", batch.Notes);
        Assert.Equal("mayordomo 1", batch.RecordedByLabel);
    }
}
