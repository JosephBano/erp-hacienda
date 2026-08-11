using FluentValidation;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Items;

public record InventoryItemDetailDto(Guid Id, string Name, ItemCategory Category, string Unit, decimal MinStock, string? Description, Guid? FeedStageId);
public record UnitConversionDto(Guid Id, string FromUnit, string ToUnit, decimal Factor);

public record GetInventoryItemByIdQuery(Guid InventoryItemId) : IRequest<InventoryItemDetailDto>;
public class GetInventoryItemByIdHandler(IInventoryDbContext dbContext) : IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDetailDto>
{
    public async Task<InventoryItemDetailDto> Handle(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");
        return new(item.Id, item.Name, item.Category, item.Unit, item.MinStock, item.Description, item.FeedStageId);
    }
}

public record GetInventoryBatchesQuery(Guid InventoryItemId) : IRequest<List<InventoryBatchDto>>;
public class GetInventoryBatchesHandler(IInventoryDbContext dbContext) : IRequestHandler<GetInventoryBatchesQuery, List<InventoryBatchDto>>
{
    public async Task<List<InventoryBatchDto>> Handle(GetInventoryBatchesQuery request, CancellationToken cancellationToken)
    {
        await EnsureItemExists(request.InventoryItemId, cancellationToken);
        return await dbContext.InventoryBatches.AsNoTracking().Where(b => b.InventoryItemId == request.InventoryItemId)
            .OrderBy(b => b.ExpirationDate == null).ThenBy(b => b.ExpirationDate)
            .Select(b => new InventoryBatchDto(b.Id, b.BatchNumber, b.Quantity, b.CostPerUnit, b.ExpirationDate)).ToListAsync(cancellationToken);
    }
    private async Task EnsureItemExists(Guid id, CancellationToken ct) => _ = await dbContext.InventoryItems.AsNoTracking().AnyAsync(i => i.Id == id, ct)
        ? true : throw new KeyNotFoundException($"El ítem de inventario con ID '{id}' no existe.");
}

public record GetInventoryUnitConversionsQuery(Guid InventoryItemId) : IRequest<List<UnitConversionDto>>;
public class GetInventoryUnitConversionsHandler(IInventoryDbContext dbContext) : IRequestHandler<GetInventoryUnitConversionsQuery, List<UnitConversionDto>>
{
    public async Task<List<UnitConversionDto>> Handle(GetInventoryUnitConversionsQuery request, CancellationToken cancellationToken)
    {
        if (!await dbContext.InventoryItems.AsNoTracking().AnyAsync(i => i.Id == request.InventoryItemId, cancellationToken))
            throw new KeyNotFoundException($"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");
        return await dbContext.UnitConversions.AsNoTracking().Where(c => c.InventoryItemId == request.InventoryItemId)
            .OrderBy(c => c.FromUnit).ThenBy(c => c.ToUnit)
            .Select(c => new UnitConversionDto(c.Id, c.FromUnit, c.ToUnit, c.Factor)).ToListAsync(cancellationToken);
    }
}

public record SetInventoryItemFeedStageCommand(Guid InventoryItemId, Guid? FeedStageId) : IRequest;
public class SetInventoryItemFeedStageValidator : FluentValidation.AbstractValidator<SetInventoryItemFeedStageCommand>
{
    public SetInventoryItemFeedStageValidator() => RuleFor(x => x.InventoryItemId).NotEmpty();
}
public class SetInventoryItemFeedStageHandler(IInventoryDbContext dbContext) : IRequestHandler<SetInventoryItemFeedStageCommand>
{
    public async Task Handle(SetInventoryItemFeedStageCommand request, CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");
        if (request.FeedStageId.HasValue && !await dbContext.FeedStages.AnyAsync(s => s.Id == request.FeedStageId.Value, cancellationToken))
            throw new KeyNotFoundException($"La etapa de alimento con ID '{request.FeedStageId}' no existe.");
        item.SetFeedStage(request.FeedStageId);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
