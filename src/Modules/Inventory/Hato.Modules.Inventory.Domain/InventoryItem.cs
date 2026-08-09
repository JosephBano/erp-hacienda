using Hato.SharedKernel;

namespace Hato.Modules.Inventory.Domain;

/// <summary>
/// An inventory item (medicine, feed batch, supply, product) (Art. 4 + GLOSSARY.md).
/// </summary>
public class InventoryItem : AuditableEntity
{
    private readonly List<InventoryBatch> _batches = [];

    public string Name { get; private set; }
    public ItemCategory Category { get; private set; }
    public string Unit { get; private set; }
    public decimal MinStock { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// Feeding stage (PLAN-FASE-3-5-PORCINO.md sec.3.5a.5 task 3): preiniciador, iniciador,
    /// crecimiento, engorde, gestación, lactancia — see <see cref="FeedStage"/>. Only
    /// meaningful for items of <see cref="ItemCategory.Feed"/>; the invariant is enforced in
    /// <see cref="Create"/>, not left to callers (Art. 8: the classification is data, but the
    /// rule that restricts it to feed items is domain, not UI).
    /// </summary>
    public Guid? FeedStageId { get; private set; }

    public IReadOnlyCollection<InventoryBatch> Batches => _batches.AsReadOnly();

    private InventoryItem()
    {
        Name = null!;
        Unit = null!;
    }

    private InventoryItem(
        string name, ItemCategory category, string unit, decimal minStock, string? description, Guid? feedStageId)
    {
        Name = name;
        Category = category;
        Unit = unit;
        MinStock = minStock;
        Description = description;
        FeedStageId = feedStageId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static InventoryItem Create(
        string name,
        ItemCategory category,
        string unit,
        decimal minStock = 0,
        string? description = null,
        Guid? feedStageId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del ítem de inventario no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("La unidad de medida es requerida.");

        if (minStock < 0)
            throw new DomainException("El stock mínimo no puede ser negativo.");

        if (feedStageId.HasValue && category != ItemCategory.Feed)
            throw new DomainException(
                "La etapa de alimento (feed_stage) solo aplica a ítems de categoría 'Feed'.");

        return new InventoryItem(name.Trim(), category, unit.Trim(), minStock, description?.Trim(), feedStageId);
    }

    /// <summary>
    /// Changes (or clears, with <c>null</c>) the feeding stage of an existing item. Same
    /// invariant as <see cref="Create"/>: a non-<see cref="ItemCategory.Feed"/> item cannot
    /// declare a stage.
    /// </summary>
    public void SetFeedStage(Guid? feedStageId)
    {
        if (feedStageId.HasValue && Category != ItemCategory.Feed)
            throw new DomainException(
                "La etapa de alimento (feed_stage) solo aplica a ítems de categoría 'Feed'.");

        FeedStageId = feedStageId;
    }

    public InventoryBatch AddBatch(string batchNumber, decimal quantity, decimal costPerUnit, DateOnly? expirationDate = null)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new DomainException("El número de lote no puede estar vacío.");

        if (quantity <= 0)
            throw new DomainException("La cantidad ingresada debe ser mayor a cero.");

        if (costPerUnit < 0)
            throw new DomainException("El costo unitario no puede ser negativo.");

        var batch = new InventoryBatch(Id, batchNumber.Trim(), quantity, costPerUnit, expirationDate);
        _batches.Add(batch);
        return batch;
    }
}

public class InventoryBatch : AuditableEntity
{
    public Guid InventoryItemId { get; private set; }
    public string BatchNumber { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal CostPerUnit { get; private set; }
    public DateOnly? ExpirationDate { get; private set; }

    private InventoryBatch()
    {
        BatchNumber = null!;
    }

    internal InventoryBatch(Guid inventoryItemId, string batchNumber, decimal quantity, decimal costPerUnit, DateOnly? expirationDate)
    {
        if (inventoryItemId == Guid.Empty)
            throw new DomainException("El lote debe estar vinculado a un ítem válido.");

        InventoryItemId = inventoryItemId;
        BatchNumber = batchNumber;
        Quantity = quantity;
        CostPerUnit = costPerUnit;
        ExpirationDate = expirationDate;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void DeductQuantity(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("La cantidad a consumir debe ser mayor a cero.");

        if (amount > Quantity)
            throw new DomainException($"Stock insuficiente en el lote '{BatchNumber}'. Disponible: {Quantity}, requerido: {amount}.");

        Quantity -= amount;
    }
}
