using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.MortalityCauses;

public record MotherMortalityDto(Guid MotherId, int DeathCount);

/// <summary>
/// Pre-weaning mortality per mother (sec.4.6 will consume this). "Pre-weaning" needs no
/// age cutoff or nursing-cohort lookup: an individually-subject Disposal is only possible
/// while the piglet is still identifiable, and ADR-0015 sec.5 makes that exactly the
/// lactation window — once mixed into a Headcount lot, its deaths are recorded as
/// GroupMortality instead, with no single animal to point at.
/// </summary>
public record GetPreweaningMortalityByMotherQuery(Guid? MotherId = null) : IRequest<List<MotherMortalityDto>>;

public class GetPreweaningMortalityByMotherHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetPreweaningMortalityByMotherQuery, List<MotherMortalityDto>>
{
    public async Task<List<MotherMortalityDto>> Handle(
        GetPreweaningMortalityByMotherQuery request, CancellationToken cancellationToken)
    {
        var deaths =
            from e in dbContext.AnimalEvents.AsNoTracking()
            where e.EventType == EventType.Disposal && e.AnimalId != null
            join a in dbContext.Animals.AsNoTracking().IgnoreQueryFilters()
                on e.AnimalId!.Value equals a.Id
            where a.MotherId != null
            select a.MotherId!.Value;

        if (request.MotherId is { } motherId)
        {
            deaths = deaths.Where(m => m == motherId);
        }

        return await deaths
            .GroupBy(m => m)
            .Select(g => new MotherMortalityDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
    }
}
