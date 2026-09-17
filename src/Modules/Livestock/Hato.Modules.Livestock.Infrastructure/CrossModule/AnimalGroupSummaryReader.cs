using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

/// <summary>
/// One-round-trip batched lookup of group names by id, used by the Inventory
/// consumption-history panel to label each row with the lot that ate the feed.
/// The implementation is a single SQL query with the id list — EF Core 8
/// translates the <c>Contains</c> over a parameter list to <c>ANY (@__ids)</c>,
/// so the wire shape stays one statement regardless of how many groups the
/// caller asks for.
/// </summary>
public class AnimalGroupSummaryReader(LivestockDbContext dbContext) : IAnimalGroupSummaryReader
{
    public async Task<IReadOnlyDictionary<Guid, AnimalGroupSummary>> GetByIdsAsync(
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return new Dictionary<Guid, AnimalGroupSummary>();
        }

        var rows = await dbContext.AnimalGroups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id) && g.DeletedAt == null)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(g => g.Id, g => new AnimalGroupSummary(g.Id, g.Name));
    }
}