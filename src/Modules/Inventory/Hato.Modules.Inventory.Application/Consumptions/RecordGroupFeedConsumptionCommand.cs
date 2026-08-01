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
            request.ConsumedAt,
            request.RecordedBy,
            request.BatchId,
            request.Notes);

        dbContext.GroupFeedConsumptions.Add(consumption);
        await dbContext.SaveChangesAsync(cancellationToken);

        return consumption.Id;
    }
}
