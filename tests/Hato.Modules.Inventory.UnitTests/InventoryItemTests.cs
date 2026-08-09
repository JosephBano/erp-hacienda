using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Inventory.UnitTests;

public class InventoryItemTests
{
    [Fact]
    public void CreateItem_WithValidData_Succeeds()
    {
        var item = InventoryItem.Create("Oxitetraciclina", ItemCategory.Medicine, "ml", 100);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Oxitetraciclina", item.Name);
        Assert.Equal(ItemCategory.Medicine, item.Category);
        Assert.Equal("ml", item.Unit);
        Assert.Equal(100, item.MinStock);
        Assert.Empty(item.Batches);
    }

    [Fact]
    public void AddBatch_AndDeductQuantity_UpdatesStock()
    {
        var item = InventoryItem.Create("Balanceado Lechería", ItemCategory.Feed, "kg", 500);

        var batch = item.AddBatch("LOT-2026-01", 1000, 0.45m, new DateOnly(2026, 12, 31));

        Assert.Single(item.Batches);
        Assert.Equal(1000, batch.Quantity);

        batch.DeductQuantity(250);

        Assert.Equal(750, batch.Quantity);
    }

    [Fact]
    public void DeductQuantity_ExceedingAvailableStock_Throws()
    {
        var item = InventoryItem.Create("Balanceado Lechería", ItemCategory.Feed, "kg");
        var batch = item.AddBatch("LOT-01", 100, 0.50m);

        Assert.Throws<DomainException>(() => batch.DeductQuantity(150));
    }

    [Fact]
    public void RecordGroupFeedConsumption_ValidData_Succeeds()
    {
        var groupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var consumption = GroupFeedConsumption.Record(
            groupId, itemId, 120.5m, "kg", 120.5m, 1m, new DateOnly(2026, 8, 1), "ordeñador 1");

        Assert.NotEqual(Guid.Empty, consumption.Id);
        Assert.Equal(120.5m, consumption.QuantityRecorded);
        Assert.Equal(120.5m, consumption.QuantityInBaseUnit);
        Assert.Equal("kg", consumption.UnitRecorded);
        Assert.Equal("ordeñador 1", consumption.RecordedBy);
    }

    [Fact]
    public void RecordGroupFeedConsumption_NegativeOrZeroQuantity_Throws()
    {
        Assert.Throws<DomainException>(() => GroupFeedConsumption.Record(
            Guid.NewGuid(), Guid.NewGuid(), 0m, "kg", 0m, 1m,
            new DateOnly(2026, 8, 1), "ordeñador"));
        Assert.Throws<DomainException>(() => GroupFeedConsumption.Record(
            Guid.NewGuid(), Guid.NewGuid(), -1m, "kg", -1m, 1m,
            new DateOnly(2026, 8, 1), "ordeñador"));
    }

    [Fact]
    public void RecordGroupFeedConsumption_EmptyUnit_Throws()
    {
        Assert.Throws<DomainException>(() => GroupFeedConsumption.Record(
            Guid.NewGuid(), Guid.NewGuid(), 1m, "", 1m, 1m,
            new DateOnly(2026, 8, 1), "ordeñador"));
    }

    [Fact]
    public void RecordGroupFeedConsumption_PreservesRecordedAndBaseBoth()
    {
        // 3 sacos de 40 kg → 120 kg en base. Both numbers must survive on the row.
        var consumption = GroupFeedConsumption.Record(
            Guid.NewGuid(), Guid.NewGuid(), 3m, "saco40kg", 120m, 40m,
            new DateOnly(2026, 8, 1), "ordeñador");

        Assert.Equal(3m, consumption.QuantityRecorded);
        Assert.Equal("saco40kg", consumption.UnitRecorded);
        Assert.Equal(120m, consumption.QuantityInBaseUnit);
        Assert.Equal(40m, consumption.AppliedFactor);
    }

    // PLAN-FASE-3-5-PORCINO.md sec.3.5a.5 task 3: feed_stage only applies to Feed items.

    [Fact]
    public void CreateItem_FeedCategory_WithFeedStage_Succeeds()
    {
        var stageId = Guid.NewGuid();

        var item = InventoryItem.Create("Preiniciador porcino", ItemCategory.Feed, "kg", 0, null, stageId);

        Assert.Equal(stageId, item.FeedStageId);
    }

    [Fact]
    public void CreateItem_NonFeedCategory_WithFeedStage_Throws()
    {
        var stageId = Guid.NewGuid();

        Assert.Throws<DomainException>(() =>
            InventoryItem.Create("Oxitetraciclina", ItemCategory.Medicine, "ml", 0, null, stageId));
    }

    [Fact]
    public void CreateItem_WithoutFeedStage_LeavesItNull()
    {
        var item = InventoryItem.Create("Balanceado genérico", ItemCategory.Feed, "kg");

        Assert.Null(item.FeedStageId);
    }

    [Fact]
    public void SetFeedStage_OnFeedItem_Succeeds()
    {
        var item = InventoryItem.Create("Balanceado genérico", ItemCategory.Feed, "kg");
        var stageId = Guid.NewGuid();

        item.SetFeedStage(stageId);

        Assert.Equal(stageId, item.FeedStageId);
    }

    [Fact]
    public void SetFeedStage_OnFeedItem_WithNull_ClearsIt()
    {
        var item = InventoryItem.Create("Balanceado genérico", ItemCategory.Feed, "kg", 0, null, Guid.NewGuid());

        item.SetFeedStage(null);

        Assert.Null(item.FeedStageId);
    }

    [Fact]
    public void SetFeedStage_OnNonFeedItem_Throws()
    {
        var item = InventoryItem.Create("Oxitetraciclina", ItemCategory.Medicine, "ml");

        Assert.Throws<DomainException>(() => item.SetFeedStage(Guid.NewGuid()));
    }

    [Fact]
    public void SetFeedStage_OnNonFeedItem_WithNull_DoesNotThrow()
    {
        // Clearing (or leaving unset) never conflicts with the invariant — only
        // declaring a stage on a non-Feed item does.
        var item = InventoryItem.Create("Oxitetraciclina", ItemCategory.Medicine, "ml");

        item.SetFeedStage(null);

        Assert.Null(item.FeedStageId);
    }
}
