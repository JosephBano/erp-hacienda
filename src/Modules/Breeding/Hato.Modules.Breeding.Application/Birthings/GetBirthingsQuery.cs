using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Livestock.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Birthings;

/// <summary>
/// Lists registered birthings for the panel "Partos" tab. Returns each birthing
/// with its dam's farm tag and the offspring rows that <c>RecordBirthing</c>
/// created — the offspring rows in particular carry the per-calf initial weight,
/// which is the data point the user explicitly asked for. Ordered most-recent-first
/// to match the panel's "Lo que registré hoy" mental model
/// (PLAN-FASE-3-5-PORCINO.md sec.2.3).
/// </summary>
public record GetBirthingsQuery() : IRequest<List<BirthingListItemDto>>;

public class GetBirthingsQueryHandler(
    IBreedingDbContext dbContext,
    IBirthingOffspringReader offspringReader)
    : IRequestHandler<GetBirthingsQuery, List<BirthingListItemDto>>
{
    public async Task<List<BirthingListItemDto>> Handle(GetBirthingsQuery request, CancellationToken cancellationToken)
    {
        // First trip: pull every non-deleted birthing we want to show, ordered by
        // recency. The query filter is by DeletedAt alone — the panel currently has
        // no date / dam filters, and adding them would require querystring params
        // the spec does not demand yet. A simple list endpoint is exactly what the
        // user asked for.
        var birthings = await dbContext.Birthings
            .AsNoTracking()
            .Where(b => b.DeletedAt == null)
            .OrderByDescending(b => b.BirthDate)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                b.DamId,
                b.PregnancyId,
                b.BirthDate,
                b.Difficulty,
                b.TotalBorn,
                b.BornAlive,
                b.BornDead,
                b.Mummified,
                b.LitterWeight,
                b.Notes,
                b.NursingCohortId,
                b.WeanedAt,
                b.WeanedCount,
                b.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (birthings.Count == 0)
        {
            return new List<BirthingListItemDto>();
        }

        // Cross-module read: dam farm tags + offspring rows. The contract lives in
        // Livestock.Contracts so Breeding does not depend on Livestock.Domain or
        // Livestock.Application.Abstractions (Art. 6).
        var pairs = birthings.Select(b => new BirthingDamPair(b.Id, b.DamId)).ToList();
        var bundles = await offspringReader.GetForBirthingPairsAsync(pairs, cancellationToken);

        return birthings.Select(b =>
        {
            var bundle = bundles.TryGetValue(b.Id, out var found) ? found : null;
            var offspring = bundle?.Offspring
                .Select(o => new BirthingListOffspringDto(
                    o.AnimalId,
                    o.FarmTag,
                    o.Sex,
                    o.BirthWeightKg))
                .ToList() ?? new List<BirthingListOffspringDto>();

            return new BirthingListItemDto(
                b.Id,
                b.DamId,
                bundle?.DamFarmTag,
                b.BirthDate,
                b.Difficulty.ToString(),
                b.TotalBorn,
                b.BornAlive,
                b.BornDead,
                b.Mummified,
                b.LitterWeight,
                b.Notes,
                b.NursingCohortId,
                b.WeanedAt,
                b.WeanedCount,
                offspring);
        }).ToList();
    }
}
