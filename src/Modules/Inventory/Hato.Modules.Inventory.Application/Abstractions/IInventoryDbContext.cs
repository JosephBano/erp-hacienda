using Hato.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.Abstractions;

public interface IInventoryDbContext
{
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<InventoryBatch> InventoryBatches { get; }
    DbSet<GroupFeedConsumption> GroupFeedConsumptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
