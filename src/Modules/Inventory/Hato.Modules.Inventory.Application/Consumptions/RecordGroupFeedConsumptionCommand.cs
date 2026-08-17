using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Consumptions;

/// <summary>
/// <paramref name="Unit"/> is the unit the operator typed. It is optional on purpose:
/// callers that predate the conversions of 3.5a.5 send a bare quantity and mean the item's
/// own unit, which is exactly what they always meant. Making it mandatory turned every one
/// of those calls into a 400 — a breaking change nobody asked for, and one the field app
/// would have discovered in the paddock.
/// </summary>
public record RecordGroupFeedConsumptionCommand(
    Guid GroupId,
    Guid InventoryItemId,
    decimal Quantity,
    DateOnly ConsumedAt,
    string RecordedBy,
    string? Unit = null,
    Guid? BatchId = null,
    string? Notes = null) : IRequest<Guid>;

public class RecordGroupFeedConsumptionValidator : AbstractValidator<RecordGroupFeedConsumptionCommand>
{
    public RecordGroupFeedConsumptionValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).MaximumLength(20).When(x => x.Unit is not null);
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
    }
}

public class RecordGroupFeedConsumptionHandler(IInventoryDbContext dbContext)
    : IRequestHandler<RecordGroupFeedConsumptionCommand, Guid>
{
    public async Task<Guid> Handle(RecordGroupFeedConsumptionCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken);

        if (item is null)
            throw new DomainException($"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");

        // Bug from the Fase 3.5 retrospective / docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.5:
        // the operator records "3 sacos de 40 kg" and the system silently stores
        // the literal number 3 against the item whose base unit is kg. The
        // cost-prorate engine then reads 3 kg of feed for 42 pigs and the
        // numbers stop making sense. Resolve the conversion here, in the
        // handler that owns the write, so both views survive.
        // No unit means "the item's own unit": that is what every caller written before
        // conversions existed was already saying.
        var unitRecorded = string.IsNullOrWhiteSpace(request.Unit) ? item.Unit : request.Unit.Trim();

        decimal appliedFactor;
        decimal quantityInBaseUnit;
        if (string.Equals(unitRecorded, item.Unit, StringComparison.OrdinalIgnoreCase))
        {
            appliedFactor = 1m;
            quantityInBaseUnit = request.Quantity;
        }
        else
        {
            var conversion = await dbContext.UnitConversions
                .FirstOrDefaultAsync(c =>
                    c.InventoryItemId == request.InventoryItemId
                    && c.FromUnit == unitRecorded
                    && c.ToUnit == item.Unit,
                    cancellationToken);

            if (conversion is null)
            {
                throw new DomainException(
                    $"No hay conversión definida para '{unitRecorded}' → '{item.Unit}' " +
                    $"en el ítem '{item.Name}'. Configure la conversión antes de registrar el consumo.");
            }

            appliedFactor = conversion.Factor;
            quantityInBaseUnit = Math.Round(request.Quantity * conversion.Factor, 3, MidpointRounding.ToEven);
        }

        // The batch is decremented in the item's base unit, which is the only unit a
        // batch has: InventoryItem carries a single Unit and the batch quantity is
        // expressed in it. Deducting the typed number instead would take 3 kg out of
        // stock when 3 sacks of 40 kg left the store — the "bug del saco" this branch
        // exists to close, one line below the conversion that closes it.
        //
        // FIFO when no batch is specified (feature/inventory-consumption-history B.2):
        // a consumption without a batch_id used to be a write-only event — the row was
        // recorded but the stock was untouched, so the panel's "cuánto bajó" question
        // had no honest answer. Draining the oldest batch first matches what a real
        // warehouse does ("agarrá lo que entró primero"), and when the consumption
        // spans more than one batch, the remainder walks the next-oldest.
        DeductFromBatchesFifo(item, quantityInBaseUnit, request.BatchId);

        var consumptionBatchId = ResolveConsumptionBatchId(item, request.BatchId);

        var consumption = GroupFeedConsumption.Record(
            request.GroupId,
            request.InventoryItemId,
            request.Quantity,
            unitRecorded,
            quantityInBaseUnit,
            appliedFactor,
            request.ConsumedAt,
            request.RecordedBy,
            consumptionBatchId,
            request.Notes);

        dbContext.GroupFeedConsumptions.Add(consumption);
        await dbContext.SaveChangesAsync(cancellationToken);

        return consumption.Id;
    }

    /// <summary>
    /// Deducts <paramref name="quantityInBaseUnit"/> from the item's batches in FIFO
    /// order. When <paramref name="explicitBatchId"/> is supplied the call is restricted
    /// to that single batch (legacy behaviour, unchanged). When null, the oldest batch
    /// with stock is drained first; if the consumption spans more than one batch, the
    /// remainder walks the next-oldest until either satisfied or the warehouse runs out.
    ///
    /// The pre-change behaviour — record the consumption but touch no stock when
    /// <c>batchId</c> was null — was the bug <see cref="GetInventoryConsumptionsQuery"/>
    /// exposes from the panel: a user could see "se consumieron 10 kg" without any
    /// change to the live stock. The domain throws (not the handler) when the total
    /// stock is insufficient, so the consumption row is never written for a request
    /// the system could not actually fulfil.
    /// </summary>
    private static void DeductFromBatchesFifo(
        InventoryItem item, decimal quantityInBaseUnit, Guid? explicitBatchId)
    {
        if (explicitBatchId.HasValue)
        {
            var batch = item.Batches.FirstOrDefault(b => b.Id == explicitBatchId.Value)
                ?? throw new DomainException($"El lote con ID '{explicitBatchId.Value} no existe en el ítem.");

            batch.DeductQuantity(quantityInBaseUnit);
            return;
        }

        var ordered = item.Batches
            .Where(b => b.Quantity > 0)
            .OrderBy(b => b.ReceivedAt)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        if (ordered.Count == 0)
        {
            throw new DomainException(
                $"El ítem '{item.Name}' no tiene lotes con stock disponible para registrar el consumo.");
        }

        var remaining = quantityInBaseUnit;
        foreach (var batch in ordered)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, batch.Quantity);
            batch.DeductQuantity(take);
            remaining -= take;
        }

        if (remaining > 0)
        {
            throw new DomainException(
                $"Stock insuficiente en '{item.Name}'. Disponible total: " +
                $"{quantityInBaseUnit - remaining} {item.Unit}, requerido: {quantityInBaseUnit} {item.Unit}.");
        }
    }

    /// <summary>
    /// The batch id stored on the consumption row reflects what actually drained
    /// stock. When the caller picked a specific batch, that one. When the FIFO path
    /// ran, the row carries the **first** batch that lost stock — the same one the
    /// operator would think of as "el lote que se comió" — so the panel can show a
    /// meaningful value rather than null.
    /// </summary>
    private static Guid? ResolveConsumptionBatchId(InventoryItem item, Guid? explicitBatchId)
    {
        if (explicitBatchId.HasValue) return explicitBatchId;
        return item.Batches
            .Where(b => b.Quantity > 0)
            .OrderBy(b => b.ReceivedAt)
            .ThenBy(b => b.CreatedAt)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefault();
    }
}
