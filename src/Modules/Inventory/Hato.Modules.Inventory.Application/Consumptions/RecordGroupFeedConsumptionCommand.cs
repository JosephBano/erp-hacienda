using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Consumptions;

public record RecordGroupFeedConsumptionCommand(
    Guid GroupId,
    Guid InventoryItemId,
    decimal Quantity,
    string Unit,
    DateOnly ConsumedAt,
    string RecordedBy,
    Guid? BatchId = null,
    string? Notes = null) : IRequest<Guid>;

public class RecordGroupFeedConsumptionValidator : AbstractValidator<RecordGroupFeedConsumptionCommand>
{
    public RecordGroupFeedConsumptionValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
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

        // Bug from the Fase 3.5 retrospective / PLAN-FASE-3-5-PORCINO.md sec.3.5a.5:
        // the operator records "3 sacos de 40 kg" and the system silently stores
        // the literal number 3 against the item whose base unit is kg. The
        // cost-prorate engine then reads 3 kg of feed for 42 pigs and the
        // numbers stop making sense. Resolve the conversion here, in the
        // handler that owns the write, so both views survive.
        decimal appliedFactor;
        decimal quantityInBaseUnit;
        if (string.Equals(request.Unit, item.Unit, StringComparison.OrdinalIgnoreCase))
        {
            appliedFactor = 1m;
            quantityInBaseUnit = request.Quantity;
        }
        else
        {
            var conversion = await dbContext.UnitConversions
                .FirstOrDefaultAsync(c =>
                    c.InventoryItemId == request.InventoryItemId
                    && c.FromUnit == request.Unit
                    && c.ToUnit == item.Unit,
                    cancellationToken);

            if (conversion is null)
            {
                throw new DomainException(
                    $"No hay conversión definida para '{request.Unit}' → '{item.Unit}' " +
                    $"en el ítem '{item.Name}'. Configure la conversión antes de registrar el consumo.");
            }

            appliedFactor = conversion.Factor;
            quantityInBaseUnit = Math.Round(request.Quantity * conversion.Factor, 3, MidpointRounding.ToEven);
        }

        // The batch is decremented in the unit the operator actually consumed — that's
        // what came off the stack of bags. The base-unit column on the consumption row
        // carries the converted number for the cost engine.
        if (request.BatchId.HasValue)
        {
            var batch = item.Batches.FirstOrDefault(b => b.Id == request.BatchId.Value);
            if (batch is null)
                throw new DomainException($"El lote con ID '{request.BatchId.Value}' no existe en el ítem.");

            batch.DeductQuantity(request.Quantity);
        }

        var consumption = GroupFeedConsumption.Record(
            request.GroupId,
            request.InventoryItemId,
            request.Quantity,
            request.Unit,
            quantityInBaseUnit,
            appliedFactor,
            request.ConsumedAt,
            request.RecordedBy,
            request.BatchId,
            request.Notes);

        dbContext.GroupFeedConsumptions.Add(consumption);
        await dbContext.SaveChangesAsync(cancellationToken);

        return consumption.Id;
    }
}
