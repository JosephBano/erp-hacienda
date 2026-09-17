using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Contracts;
using Hato.Modules.Livestock.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Consumptions;

/// <summary>
/// Lists the consumptions recorded against a given inventory item, most-recent-first.
/// Powers the "Consumos registrados" section of the inventory item detail panel
/// (<c>feature/inventory-consumption-history</c>).
///
/// Two round-trips no matter how many consumptions the item has:
/// one for the consumption rows themselves (own module), one for the lot names
/// (cross-module via <see cref="IAnimalGroupSummaryReader"/>). The DTO carries
/// the resolved group name denormalised so the panel does no further lookups.
/// </summary>
public record GetInventoryConsumptionsQuery(Guid InventoryItemId) : IRequest<List<InventoryConsumptionListItemDto>>;

public class GetInventoryConsumptionsQueryHandler(
    IInventoryDbContext dbContext,
    IAnimalGroupSummaryReader groupReader)
    : IRequestHandler<GetInventoryConsumptionsQuery, List<InventoryConsumptionListItemDto>>
{
    public async Task<List<InventoryConsumptionListItemDto>> Handle(
        GetInventoryConsumptionsQuery request,
        CancellationToken cancellationToken)
    {
        // First, confirm the item exists and is not tombstoned. 404 means "this
        // id does not exist in the catalog", NOT "this id has no consumptions yet".
        var item = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.Id == request.InventoryItemId && i.DeletedAt == null)
            .Select(i => new { i.Id, i.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            // KeyNotFoundException maps to 404 in ApiExceptionHandler (Hato.Api/ApiExceptionHandler.cs:27).
            throw new KeyNotFoundException(
                $"El ítem de inventario con ID '{request.InventoryItemId}' no existe.");
        }

        var rows = await dbContext.GroupFeedConsumptions
            .AsNoTracking()
            .Where(c => c.InventoryItemId == request.InventoryItemId)
            .OrderByDescending(c => c.ConsumedAt)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.GroupId,
                c.QuantityRecorded,
                c.UnitRecorded,
                c.QuantityInBaseUnit,
                c.AppliedFactor,
                c.BatchId,
                c.ConsumedAt,
                c.RecordedByLabel,
                c.Notes
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new List<InventoryConsumptionListItemDto>();
        }

        // Cross-module lookup of group names in one round-trip.
        var groupIds = rows.Select(r => r.GroupId).Distinct().ToList();
        var groupMap = await groupReader.GetByIdsAsync(groupIds, cancellationToken);

        return rows.Select(r => new InventoryConsumptionListItemDto(
            r.Id,
            r.GroupId,
            groupMap.TryGetValue(r.GroupId, out var g) ? g.Name : r.GroupId.ToString(),
            item.Id,
            item.Name,
            r.QuantityRecorded,
            r.UnitRecorded,
            r.QuantityInBaseUnit,
            r.AppliedFactor,
            r.BatchId,
            r.ConsumedAt,
            r.RecordedByLabel,
            r.Notes)).ToList();
    }
}