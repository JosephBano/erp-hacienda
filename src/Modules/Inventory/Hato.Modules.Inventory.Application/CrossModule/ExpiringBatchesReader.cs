using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Inventory.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.CrossModule;

public class ExpiringBatchesReader(IInventoryDbContext dbContext) : IExpiringBatchesReader
{
    public async Task<IReadOnlyList<ExpiringBatchDto>> GetExpiringWithinAsync(DateOnly thresholdDate, CancellationToken cancellationToken)
    {
        return await dbContext.InventoryBatches
            .AsNoTracking()
            .Where(b => b.ExpirationDate != null && b.ExpirationDate <= thresholdDate && b.Quantity > 0)
            .Join(dbContext.InventoryItems.AsNoTracking(),
                b => b.InventoryItemId,
                i => i.Id,
                (b, i) => new ExpiringBatchDto(b.Id, i.Id, i.Name, b.BatchNumber, b.ExpirationDate!.Value, b.Quantity))
            .ToListAsync(cancellationToken);
    }
}
