using Hato.SharedKernel;

namespace Hato.Modules.Inventory.Domain;

/// <summary>
/// Feed consumption recorded at the group/lot level (Art. 4 + ARCHITECTURE.md).
/// Feed costs are registered by group and prorated per animal-day.
/// </summary>
public class GroupFeedConsumption : AuditableEntity
{
    public Guid GroupId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public Guid? BatchId { get; private set; }

    /// <summary>
    /// The quantity the operator typed — in whatever unit they typed it ("3 sacos de 40
    /// kg", "12.5 kg", "una paca"). Preserved verbatim because rounding away the unit
    /// is exactly the lossy transformation the macro plan called out as the bug to fix.
    /// </summary>
    public decimal QuantityRecorded { get; private set; }

    /// <summary>
    /// The unit the operator typed (e.g. <c>saco40kg</c>, <c>kg</c>). The display unit of
    /// the inventory item's base unit is its <see cref="InventoryItem.Unit"/>; this field
    /// keeps the original on the record so the operator can audit their own entry.
    /// </summary>
    public string UnitRecorded { get; private set; }

    /// <summary>
    /// The same quantity expressed in the inventory item's base unit (the unit on the
    /// <see cref="InventoryItem.Unit"/> column). This is what the cost-prorate engine
    /// reads: kilograms of feed consumed by the group on the day, regardless of
    /// whether the operator typed "3 sacos" or "120 kg". <c>decimal</c> throughout
    /// because Art. 10.
    /// </summary>
    public decimal QuantityInBaseUnit { get; private set; }

    /// <summary>The conversion factor that was applied (1.0 when the operator typed the base unit).</summary>
    public decimal AppliedFactor { get; private set; }

    public DateOnly ConsumedAt { get; private set; }
    public Guid? RecordedById { get; private set; }
    public string RecordedByLabel { get; private set; }
    public string RecordedBy => RecordedByLabel;
    public string? Notes { get; private set; }

    private GroupFeedConsumption()
    {
        RecordedByLabel = null!;
        UnitRecorded = null!;
    }

    private GroupFeedConsumption(
        Guid groupId,
        Guid inventoryItemId,
        Guid? batchId,
        decimal quantityRecorded,
        string unitRecorded,
        decimal quantityInBaseUnit,
        decimal appliedFactor,
        DateOnly consumedAt,
        string recordedByLabel,
        Guid? recordedById,
        string? notes)
    {
        GroupId = groupId;
        InventoryItemId = inventoryItemId;
        BatchId = batchId;
        QuantityRecorded = quantityRecorded;
        UnitRecorded = unitRecorded;
        QuantityInBaseUnit = quantityInBaseUnit;
        AppliedFactor = appliedFactor;
        ConsumedAt = consumedAt;
        RecordedByLabel = recordedByLabel;
        RecordedById = recordedById;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a consumption where the operator typed the quantity in some unit the
    /// system already knows the conversion for (or in the item's base unit, in which
    /// case <paramref name="appliedFactor"/> is 1). The base-unit quantity is what
    /// the cost engine reads; the recorded values are kept on the row so neither the
    /// audit trail nor the next person looking at the feed book loses the original.
    /// </summary>
    public static GroupFeedConsumption Record(
        Guid groupId,
        Guid inventoryItemId,
        decimal quantityRecorded,
        string unitRecorded,
        decimal quantityInBaseUnit,
        decimal appliedFactor,
        DateOnly consumedAt,
        string recordedBy,
        Guid? batchId = null,
        string? notes = null,
        Guid? recordedById = null)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("El consumo debe estar vinculado a un grupo válido.");

        if (inventoryItemId == Guid.Empty)
            throw new DomainException("El consumo debe estar vinculado a un alimento válido.");

        if (quantityRecorded <= 0)
            throw new DomainException("La cantidad registrada debe ser mayor a cero.");

        if (quantityInBaseUnit <= 0)
            throw new DomainException("La cantidad en unidad base debe ser mayor a cero.");

        if (appliedFactor <= 0)
            throw new DomainException("El factor de conversión aplicado debe ser positivo.");

        if (string.IsNullOrWhiteSpace(unitRecorded))
            throw new DomainException("La unidad registrada no puede estar vacía.");

        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El autor del registro no puede estar vacío.");

        return new GroupFeedConsumption(
            groupId, inventoryItemId, batchId,
            quantityRecorded, unitRecorded.Trim(), quantityInBaseUnit, appliedFactor,
            consumedAt, recordedBy.Trim(), recordedById, notes?.Trim());
    }
}
