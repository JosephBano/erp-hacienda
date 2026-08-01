using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Pregnancies;

public record GetActivePregnanciesQuery() : IRequest<List<PregnancyDto>>;

public class GetActivePregnanciesQueryHandler(IBreedingDbContext dbContext)
    : IRequestHandler<GetActivePregnanciesQuery, List<PregnancyDto>>
{
    public async Task<List<PregnancyDto>> Handle(GetActivePregnanciesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Pregnancies
            .AsNoTracking()
            .Where(p => p.Status == PregnancyStatus.Active)
            .OrderBy(p => p.ExpectedBirthDate)
            .Select(p => new PregnancyDto(
                p.Id,
                p.DamId,
                p.ServiceId,
                p.ConfirmedAt,
                p.ExpectedBirthDate,
                p.Status.ToString(),
                p.Notes,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
