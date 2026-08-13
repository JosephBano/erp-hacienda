using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Items;

public record CreateInventoryItemCommand(
    string Name,
    ItemCategory Category,
    string Unit,
    decimal MinStock = 0,
    string? Description = null,
    Guid? FeedStageId = null) : IRequest<Guid>;

public class CreateInventoryItemValidator : AbstractValidator<CreateInventoryItemCommand>
{
    // MD-02: upper bound mirrors the reception validator. MinStock is a
    // threshold for low-stock alerts — same 1.000.000 ceiling protects against
    // typos that would otherwise turn the whole inventory "red" forever.
    private const decimal MaxQuantityOrCost = 1_000_000m;

    public CreateInventoryItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.MinStock).InclusiveBetween(0m, MaxQuantityOrCost);
    }
}

public class CreateInventoryItemHandler(IInventoryDbContext dbContext)
    : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        if (request.FeedStageId.HasValue)
        {
            var stageExists = await dbContext.FeedStages
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.FeedStageId.Value, cancellationToken);

            if (!stageExists)
                throw new DomainException($"La etapa de alimento con ID '{request.FeedStageId}' no existe.");
        }

        var item = InventoryItem.Create(
            request.Name, request.Category, request.Unit, request.MinStock, request.Description, request.FeedStageId);

        dbContext.InventoryItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}

public record CreateInventoryBatchCommand(
    Guid InventoryItemId, string BatchNumber, decimal Quantity, decimal CostPerUnit, DateOnly? ExpirationDate = null) : IRequest<Guid>;

public class CreateInventoryBatchValidator : AbstractValidator<CreateInventoryBatchCommand>
{
    // MD-02: same ceiling as RecordInventoryReceptionValidator. The legacy
    // /batches endpoint is kept for technical/manual adjustments per
    // ADR-0026 alternativa D — it must not become a backdoor that bypasses the
    // bounds introduced on the canonical endpoint.
    private const decimal MaxQuantityOrCost = 1_000_000m;

    public CreateInventoryBatchValidator()
    {
        RuleFor(x => x.InventoryItemId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).InclusiveBetween(0.001m, MaxQuantityOrCost);
        RuleFor(x => x.CostPerUnit).InclusiveBetween(0m, MaxQuantityOrCost);
    }
}

public class CreateInventoryBatchHandler(IInventoryDbContext dbContext)
    : IRequestHandler<CreateInventoryBatchCommand, Guid>
{
    public async Task<Guid> Handle(CreateInventoryBatchCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken);

        if (item is null)
            throw new DomainException($"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");

#pragma warning disable CS0618 // AddBatch is intentionally kept for the legacy POST /batches endpoint (ADR-0026).
        var batch = item.AddBatch(request.BatchNumber, request.Quantity, request.CostPerUnit, request.ExpirationDate);
#pragma warning restore CS0618
        dbContext.InventoryBatches.Add(batch);

        await dbContext.SaveChangesAsync(cancellationToken);

        return batch.Id;
    }
}