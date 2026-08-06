using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Species;

/// <summary>The species catalog. Without it, nothing — the admin panel included — has a way to know what to register an animal against.</summary>
public record SpeciesDto(Guid Id, string Name, int? GestationDays);

public record GetSpeciesQuery : IRequest<List<SpeciesDto>>;

public class GetSpeciesHandler(ILivestockDbContext dbContext) : IRequestHandler<GetSpeciesQuery, List<SpeciesDto>>
{
    public async Task<List<SpeciesDto>> Handle(GetSpeciesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Species
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SpeciesDto(s.Id, s.Name, s.GestationDays))
            .ToListAsync(cancellationToken);
    }
}
