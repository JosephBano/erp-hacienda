using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Breeds;

public record BreedDto(Guid Id, Guid SpeciesId, string Name);

public record GetBreedsQuery(Guid? SpeciesId = null) : IRequest<List<BreedDto>>;

public class GetBreedsHandler(ILivestockDbContext dbContext) : IRequestHandler<GetBreedsQuery, List<BreedDto>>
{
    public async Task<List<BreedDto>> Handle(GetBreedsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Breeds.AsNoTracking();

        if (request.SpeciesId is { } speciesId)
        {
            query = query.Where(b => b.SpeciesId == speciesId);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BreedDto(b.Id, b.SpeciesId, b.Name))
            .ToListAsync(cancellationToken);
    }
}
