using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

/// <summary>
/// Backs the panel "Partos" tab. For each (birthing, dam) pair it returns the dam's
/// currently-active FarmTag (null if it has none) and the list of calves a birth
/// produced. The caller passes the pairs because <c>breeding.birthings.dam_id</c>
/// is the canonical source for which dam goes with which birthing — Breeding's
/// application layer is the only place that knows.
/// </summary>
public class BirthingOffspringReader(LivestockDbContext dbContext) : IBirthingOffspringReader
{
    public async Task<IReadOnlyDictionary<Guid, BirthingOffspringBundle>> GetForBirthingPairsAsync(
        IReadOnlyCollection<BirthingDamPair> pairs,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return new Dictionary<Guid, BirthingOffspringBundle>();
        }

        var birthingIds = pairs.Select(p => p.BirthingId).ToList();
        var damIds = pairs.Select(p => p.DamId).Distinct().ToList();

        // One trip: every offspring row whose BirthingId matches the input set.
        var offspring = await dbContext.Animals
            .AsNoTracking()
            .Where(a => a.BirthingId.HasValue
                        && birthingIds.Contains(a.BirthingId.Value)
                        && a.DeletedAt == null)
            .Select(a => new { a.Id, BirthingId = a.BirthingId!.Value, a.Sex, a.BirthWeightKg })
            .ToListAsync(cancellationToken);

        var offspringIds = offspring.Select(o => o.Id).ToList();

        // Second trip: active (valid_to IS NULL) FarmTag identifiers. Two queries
        // share that offspringIds and damIds are cheap to compute but without them
        // we'd fall into a SELECT N+1 against the panel.
        var tagLookup = offspringIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.AnimalIdentifiers
                .AsNoTracking()
                .Where(i => offspringIds.Contains(i.AnimalId)
                            && i.Type == IdentifierType.FarmTag
                            && i.ValidTo == null)
                .ToDictionaryAsync(i => i.AnimalId, i => i.Value, cancellationToken);

        // The dam tags need to span damIds even when there are no offspring — a
        // count-only birthing (totals without the per-offspring grid) still has a
        // dam with a farm tag worth showing.
        var damTagLookup = await dbContext.AnimalIdentifiers
            .AsNoTracking()
            .Where(i => damIds.Contains(i.AnimalId)
                        && i.Type == IdentifierType.FarmTag
                        && i.ValidTo == null)
            .ToDictionaryAsync(i => i.AnimalId, i => i.Value, cancellationToken);

        return pairs.ToDictionary(
            p => p.BirthingId,
            p => new BirthingOffspringBundle(
                damTagLookup.TryGetValue(p.DamId, out var damTag) ? damTag : null,
                (IReadOnlyList<BirthingOffspringRow>)offspring
                    .Where(o => o.BirthingId == p.BirthingId)
                    .Select(o => new BirthingOffspringRow(
                        o.Id,
                        ((Sex)o.Sex).ToString(),
                        o.BirthWeightKg,
                        tagLookup.TryGetValue(o.Id, out var t) ? t : null))
                    .ToList()));
    }
}
