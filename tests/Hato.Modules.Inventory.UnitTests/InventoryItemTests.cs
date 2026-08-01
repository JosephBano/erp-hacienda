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
            groupId, itemId, 120.5m, new DateOnly(2026, 8, 1), "ordeñador 1");

        Assert.NotEqual(Guid.Empty, consumption.Id);
        Assert.Equal(120.5m, consumption.Quantity);
        Assert.Equal("ordeñador 1", consumption.RecordedBy);
    }
}
