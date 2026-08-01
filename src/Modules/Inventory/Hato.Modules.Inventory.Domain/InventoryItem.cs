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

    public IReadOnlyCollection<InventoryBatch> Batches => _batches.AsReadOnly();

    private InventoryItem()
    {
        Name = null!;
        Unit = null!;
    }

    private InventoryItem(string name, ItemCategory category, string unit, decimal minStock, string? description)
    {
        Name = name;
        Category = category;
        Unit = unit;
        MinStock = minStock;
        Description = description;
    }

    public static InventoryItem Create(
        string name, ItemCategory category, string unit, decimal minStock = 0, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del ítem de inventario no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("La unidad de medida es requerida.");

        if (minStock < 0)
            throw new DomainException("El stock mínimo no puede ser negativo.");

        return new InventoryItem(name.Trim(), category, unit.Trim(), minStock, description?.Trim());
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
