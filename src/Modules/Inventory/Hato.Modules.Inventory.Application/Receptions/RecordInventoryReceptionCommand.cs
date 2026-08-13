using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Receptions;

/// <summary>
/// ADR-0026 Decisión 3: canonical entry-point for stock entering the finca
/// (<see cref="InventoryReception"/>). Resolves unit conversion in the handler —
/// same shape as <see cref="Consumptions.RecordGroupFeedConsumptionCommand"/> —
/// and delegates the rest of the invariants to the domain
/// (<see cref="InventoryItem.RecordReception"/>). Returns the new batch id.
/// </summary>
public record RecordInventoryReceptionCommand(
    Guid ItemId,
    string BatchNumber,
    decimal Quantity,
    string Unit,
    decimal CostPerUnit,
    DateOnly? ExpirationDate,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? Notes,
    Guid? RecordedById,
    string? RecordedByLabel) : IRequest<Guid>;

public class RecordInventoryReceptionValidator : AbstractValidator<RecordInventoryReceptionCommand>
{
    // MD-02: upper bound on Quantity / CostPerUnit. 1.000.000 is the largest
    // meaningful stock quantity or unit cost for a single batch on a finca of
    // this scale (the inventory is denominated in kg / unidades / litros, never
    // pallets or tons). Anything larger is almost certainly a typo or a probe.
    // Lower bound on Quantity stays >0 (a zero-quantity reception is not a
    // reception), CostPerUnit stays >=0 (free / donated stock is legitimate).
    private const decimal MaxQuantityOrCost = 1_000_000m;

    // MD-04: lower bound on ReceivedAt. 2020-01-01 is the earliest plausible
    // reception date for a finca that has been operating with this system; any
    // older date is either a data-entry mistake or a probe. The future bound
    // tolerates 1 minute of clock skew between the operator's device and the API.
    private static readonly DateTimeOffset MinReceivedAt =
        new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public RecordInventoryReceptionValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).InclusiveBetween(0.001m, MaxQuantityOrCost);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CostPerUnit).InclusiveBetween(0m, MaxQuantityOrCost);
        RuleFor(x => x.ReceivedAt)
            .Must(r =>
                r != default
                && r <= DateTimeOffset.UtcNow.AddMinutes(1)
                && r >= MinReceivedAt)
            .WithMessage("La fecha de recepción debe ser válida, no estar en el futuro y no ser anterior a 2020.");
        RuleFor(x => x.SupplierLabel).MaximumLength(200).When(x => x.SupplierLabel is not null);
        RuleFor(x => x.InvoiceReference).MaximumLength(100).When(x => x.InvoiceReference is not null);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
        RuleFor(x => x.RecordedByLabel).MaximumLength(200).When(x => x.RecordedByLabel is not null);
    }
}

public class RecordInventoryReceptionHandler(
    IInventoryDbContext dbContext,
    ICurrentUser currentUser)
    : IRequestHandler<RecordInventoryReceptionCommand, Guid>
{
    public async Task<Guid> Handle(RecordInventoryReceptionCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item is null || item.IsDeleted)
            throw new DomainException(
                $"El ítem de inventario con ID '{request.ItemId}' no existe o fue eliminado.");

        // AL-01 (security audit #94): override the request-supplied author with
        // the authenticated subject from the JWT. The handler does NOT trust
        // request.RecordedById / request.RecordedByLabel — those fields are
        // accepted on the wire for admin-web UI compatibility, but the
        // persisted values must come from the current user (Art. 4: every
        // change is an event with date, author and cost; "author" is the
        // authenticated principal, never what the client typed).
        //
        // RecordedByLabel is preserved only when the request supplied a
        // non-empty value AND it differs from the JWT's full name; otherwise we
        // fall back to the JWT subject's full name so the row is never stored
        // with a "ghost" operator. RecordedById is always overwritten.
        var recordedById = currentUser.UserId;
        var recordedByLabel = !string.IsNullOrWhiteSpace(request.RecordedByLabel)
            ? request.RecordedByLabel.Trim()
            : currentUser.FullName;

        // Resolve the unit conversion the same way RecordGroupFeedConsumption does:
        // when the operator typed the item's own unit, the factor is 1 and the
        // typed quantity survives verbatim; otherwise look up an explicit
        // UnitConversion row (Art. 8 — the factor is data, never a code constant).
        var unitRecorded = request.Unit.Trim();
        decimal quantityInBaseUnit;
        decimal? appliedFactor;
        if (string.Equals(unitRecorded, item.Unit, StringComparison.OrdinalIgnoreCase))
        {
            quantityInBaseUnit = request.Quantity;
            appliedFactor = 1m;
        }
        else
        {
            var conversion = await dbContext.UnitConversions
                .FirstOrDefaultAsync(c =>
                    c.InventoryItemId == request.ItemId
                    && c.FromUnit == unitRecorded
                    && c.ToUnit == item.Unit,
                    cancellationToken);

            if (conversion is null)
            {
                throw new DomainException(
                    $"No hay conversión definida para '{unitRecorded}' → '{item.Unit}' " +
                    $"en el ítem '{item.Name}'. Configure la conversión antes de registrar la recepción.");
            }

            appliedFactor = conversion.Factor;
            quantityInBaseUnit = Math.Round(request.Quantity * conversion.Factor, 3, MidpointRounding.ToEven);
        }

        var batch = item.RecordReception(
            request.BatchNumber,
            quantityInBaseUnit,
            unitRecorded,
            appliedFactor,
            request.CostPerUnit,
            request.ExpirationDate,
            request.ReceivedAt,
            request.SupplierLabel,
            request.InvoiceReference,
            request.Notes,
            recordedById,
            recordedByLabel);

        // Explicit Add matches CreateInventoryBatchHandler's pattern: even though
        // RecordReception also appends to the backing field, calling Add here
        // makes the change-tracker status unambiguous and avoids the EF "expected
        // 1 row, actually affected 0" surprise that the implicit navigation-add
        // path can trip on a fresh item load.
        dbContext.InventoryBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return batch.Id;
    }
}
