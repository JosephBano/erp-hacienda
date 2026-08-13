using Hato.SharedKernel;

namespace Hato.Modules.Inventory.Domain.Events;

/// <summary>
/// Raised when an <see cref="InventoryReception"/> is recorded (ADR-0026 Decisión 4).
/// Carries the operator-declared reception metadata so downstream consumers can
/// reconstruct "what entered stock, when, from whom, with which invoice, by whom"
/// without having to re-join against <c>inventory_batches</c>. Published in-process
/// via MediatR (<see cref="INotification"/>); no outbox — reopens if Fase 4
/// (Purchasing) introduces a durable downstream consumer.
/// </summary>
public record InventoryReceptionRecorded(
    Guid BatchId,
    Guid ItemId,
    decimal QuantityInBaseUnit,
    string UnitRecorded,
    decimal? AppliedFactor,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? RecordedBy,
    DateTimeOffset OccurredAt = default) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt == default ? DateTimeOffset.UtcNow : OccurredAt;
}
