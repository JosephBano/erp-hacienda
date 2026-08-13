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
    public RecordInventoryReceptionValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CostPerUnit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReceivedAt)
            .Must(r => r != default && r <= DateTimeOffset.UtcNow)
            .WithMessage("La fecha de recepción debe ser válida y no estar en el futuro.");
        RuleFor(x => x.SupplierLabel).MaximumLength(200).When(x => x.SupplierLabel is not null);
        RuleFor(x => x.InvoiceReference).MaximumLength(100).When(x => x.InvoiceReference is not null);
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
        RuleFor(x => x.RecordedByLabel).MaximumLength(200).When(x => x.RecordedByLabel is not null);
    }
}

public class RecordInventoryReceptionHandler(IInventoryDbContext dbContext)
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
            request.RecordedById,
            request.RecordedByLabel);

        await dbContext.SaveChangesAsync(cancellationToken);

        return batch.Id;
    }
}
