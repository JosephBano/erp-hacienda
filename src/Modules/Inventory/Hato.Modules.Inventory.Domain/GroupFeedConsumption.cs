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
    public decimal Quantity { get; private set; }
    public DateOnly ConsumedAt { get; private set; }
    public Guid? RecordedById { get; private set; }
    public string RecordedByLabel { get; private set; }
    public string RecordedBy => RecordedByLabel;
    public string? Notes { get; private set; }

    private GroupFeedConsumption()
    {
        RecordedByLabel = null!;
    }

    private GroupFeedConsumption(
        Guid groupId,
        Guid inventoryItemId,
        Guid? batchId,
        decimal quantity,
        DateOnly consumedAt,
        string recordedByLabel,
        Guid? recordedById,
        string? notes)
    {
        GroupId = groupId;
        InventoryItemId = inventoryItemId;
        BatchId = batchId;
        Quantity = quantity;
        ConsumedAt = consumedAt;
        RecordedByLabel = recordedByLabel;
        RecordedById = recordedById;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static GroupFeedConsumption Record(
        Guid groupId,
        Guid inventoryItemId,
        decimal quantity,
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

        if (quantity <= 0)
            throw new DomainException("La cantidad consumida debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El autor del registro no puede estar vacío.");

        return new GroupFeedConsumption(
            groupId, inventoryItemId, batchId, quantity, consumedAt, recordedBy.Trim(), recordedById, notes?.Trim());
    }
}
