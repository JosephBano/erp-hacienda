using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalCategories;

public record AnimalCategoryDto(Guid Id, Guid SpeciesId, string Name);

public record GetAnimalCategoriesQuery(Guid? SpeciesId = null) : IRequest<List<AnimalCategoryDto>>;

public class GetAnimalCategoriesHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetAnimalCategoriesQuery, List<AnimalCategoryDto>>
{
    public async Task<List<AnimalCategoryDto>> Handle(GetAnimalCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.AnimalCategories.AsNoTracking();

        if (request.SpeciesId is { } speciesId)
        {
            query = query.Where(c => c.SpeciesId == speciesId);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new AnimalCategoryDto(c.Id, c.SpeciesId, c.Name))
            .ToListAsync(cancellationToken);
    }
}
