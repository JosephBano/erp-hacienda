namespace Hato.Modules.Inventory.Contracts;

/// <summary>
/// Read-side projection of <c>GroupFeedConsumption</c> for the panel
/// "Consumos registrados" section under an inventory item detail. Distinct
/// from the write-side contracts so the read shape can evolve independently
/// of the operator's payload. The <c>GroupName</c> and
/// <c>InventoryItemName</c> fields are denormalised on purpose — without
/// them the panel has to do an N+1 round-trip per row to look up labels.
/// </summary>
public record InventoryConsumptionListItemDto(
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