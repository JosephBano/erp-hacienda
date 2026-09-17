using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Items;

public record InventoryBatchDto(
    Guid Id,
    string BatchNumber,
    decimal Quantity,
    decimal CostPerUnit,
    DateOnly? ExpirationDate,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? Notes,
    string? RecordedByLabel);

public record InventoryItemDto(
    Guid Id,
    string Name,
    ItemCategory Category,
    string Unit,
    decimal MinStock,
    string? Description,
    Guid? FeedStageId,
    decimal TotalStock,
    List<InventoryBatchDto> Batches);

public record GetInventoryItemsQuery(ItemCategory? Category = null) : IRequest<List<InventoryItemDto>>;

public class GetInventoryItemsHandler(IInventoryDbContext dbContext)
    : IRequestHandler<GetInventoryItemsQuery, List<InventoryItemDto>>
{
    public async Task<List<InventoryItemDto>> Handle(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryItems
            .AsNoTracking()
            .Include(i => i.Batches)
            .AsQueryable();

        if (request.Category.HasValue)
            query = query.Where(i => i.Category == request.Category.Value);

        var items = await query.ToListAsync(cancellationToken);

        return items.Select(i => new InventoryItemDto(
            i.Id,
            i.Name,
            i.Category,
            i.Unit,
            i.MinStock,
            i.Description,
            i.FeedStageId,
            i.Batches.Sum(b => b.Quantity),
            i.Batches.Select(b => new InventoryBatchDto(
                b.Id, b.BatchNumber, b.Quantity, b.CostPerUnit, b.ExpirationDate,
                b.ReceivedAt, b.SupplierLabel, b.InvoiceReference, b.Notes, b.RecordedByLabel)).ToList()
        )).ToList();
    }
}
