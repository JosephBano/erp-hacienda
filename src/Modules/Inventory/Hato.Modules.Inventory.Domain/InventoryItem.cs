using Hato.Modules.Inventory.Domain.Events;
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
    /// Feeding stage (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.5 task 3): preiniciador, iniciador,
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

    [Obsolete("Use RecordReception(...) which captures operator-declared reception metadata (date, supplier, invoice, author) and raises InventoryReceptionRecorded. See ADR-0026.")]
    public InventoryBatch AddBatch(string batchNumber, decimal quantity, decimal costPerUnit, DateOnly? expirationDate = null)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new DomainException("El número de lote no puede estar vacío.");

        if (quantity <= 0)
            throw new DomainException("La cantidad ingresada debe ser mayor a cero.");

        if (costPerUnit < 0)
            throw new DomainException("El costo unitario no puede ser negativo.");

        // Legacy path: AddBatch is kept for technical/manual adjustments (ADR-0026
        // alternativa D). ReceivedAt defaults to the row's creation time so the
        // batch never violates the NOT NULL constraint introduced by the migration.
        var now = DateTimeOffset.UtcNow;
        var batch = new InventoryBatch(
            Id, batchNumber.Trim(), quantity, costPerUnit, expirationDate,
            now, supplierLabel: null, invoiceReference: null, notes: null,
            recordedById: null, recordedByLabel: null);
        _batches.Add(batch);
        return batch;
    }

    /// <summary>
    /// Records an inventory reception: creates a new <see cref="InventoryBatch"/> carrying
    /// the operator-declared reception metadata (date, supplier, invoice, author) and
    /// raises <see cref="InventoryReceptionRecorded"/> so downstream consumers can react
    /// (ADR-0026). This is the canonical entry-point for stock entering the finca;
    /// <see cref="AddBatch"/> is kept for technical/manual adjustments and emits no event.
    /// </summary>
    public InventoryBatch RecordReception(
        string batchNumber,
        decimal quantityInBaseUnit,
        string unitRecorded,
        decimal? appliedFactor,
        decimal costPerUnit,
        DateOnly? expirationDate,
        DateTimeOffset receivedAt,
        string? supplierLabel,
        string? invoiceReference,
        string? notes,
        Guid? recordedById,
        string? recordedByLabel)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new DomainException("El número de lote no puede estar vacío.");

        if (batchNumber.Trim().Length > 50)
            throw new DomainException("El número de lote no puede superar los 50 caracteres.");

        if (quantityInBaseUnit <= 0)
            throw new DomainException("La cantidad (en unidad base) debe ser mayor a cero.");

        if (costPerUnit < 0)
            throw new DomainException("El costo unitario no puede ser negativo.");

        if (receivedAt > DateTimeOffset.UtcNow)
            throw new DomainException("La fecha de recepción no puede estar en el futuro.");

        if (receivedAt < CreatedAt)
            throw new DomainException(
                "La fecha de recepción no puede ser anterior a la creación del ítem.");

        var batch = new InventoryBatch(
            Id, batchNumber.Trim(), quantityInBaseUnit, costPerUnit, expirationDate,
            receivedAt, supplierLabel, invoiceReference, notes,
            recordedById, recordedByLabel);

        _batches.Add(batch);

        RaiseDomainEvent(new InventoryReceptionRecorded(
            batch.Id,
            Id,
            quantityInBaseUnit,
            unitRecorded,
            appliedFactor,
            receivedAt,
            supplierLabel,
            invoiceReference,
            recordedByLabel));

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

    /// <summary>
    /// Operator-declared date the stock actually arrived at the finca (ADR-0026 Decisión 1).
    /// Distinct from <see cref="AuditableEntity.CreatedAt"/>: a batch can be created today
    /// for a reception that happened yesterday. Stored in UTC; America/Guayaquil is
    /// presentation-only (AGENTS.md rule 6).
    /// </summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Supplier in free text. Will become an FK to <c>suppliers</c> in Fase 4
    /// (Purchasing); the label is preserved on existing rows as historical truth.</summary>
    public string? SupplierLabel { get; private set; }

    /// <summary>Invoice / dispatch note reference, optional.</summary>
    public string? InvoiceReference { get; private set; }

    /// <summary>Free-text notes about the reception (anything the schema cannot capture).</summary>
    public string? Notes { get; private set; }

    /// <summary>Soft FK to <c>people.users</c> (recorded by). Same orphan pattern as
    /// <see cref="GroupFeedConsumption.RecordedById"/>: no <c>REFERENCES</c> constraint,
    /// reconciled by name when needed via <c>AuditSaveChangesInterceptor</c>.</summary>
    public Guid? RecordedById { get; private set; }

    /// <summary>Recorded-by display name (free text, mirrors <see cref="GroupFeedConsumption"/>).</summary>
    public string? RecordedByLabel { get; private set; }

    private InventoryBatch()
    {
        BatchNumber = null!;
    }

    internal InventoryBatch(
        Guid inventoryItemId,
        string batchNumber,
        decimal quantity,
        decimal costPerUnit,
        DateOnly? expirationDate,
        DateTimeOffset receivedAt,
        string? supplierLabel,
        string? invoiceReference,
        string? notes,
        Guid? recordedById,
        string? recordedByLabel)
    {
        if (inventoryItemId == Guid.Empty)
            throw new DomainException("El lote debe estar vinculado a un ítem válido.");

        InventoryItemId = inventoryItemId;
        BatchNumber = batchNumber;
        Quantity = quantity;
        CostPerUnit = costPerUnit;
        ExpirationDate = expirationDate;
        ReceivedAt = receivedAt;
        SupplierLabel = supplierLabel?.Trim();
        InvoiceReference = invoiceReference?.Trim();
        Notes = notes?.Trim();
        RecordedById = recordedById;
        RecordedByLabel = recordedByLabel?.Trim();
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
