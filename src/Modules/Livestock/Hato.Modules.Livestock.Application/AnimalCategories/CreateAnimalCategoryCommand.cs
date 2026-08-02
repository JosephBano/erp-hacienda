using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;

namespace Hato.Modules.Livestock.Application.AnimalCategories;

public record CreateAnimalCategoryCommand(Guid SpeciesId, string Name) : IRequest<Guid>;

public class CreateAnimalCategoryHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreateAnimalCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateAnimalCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = Hato.Modules.Livestock.Domain.AnimalCategory.Create(request.SpeciesId, request.Name);

        dbContext.AnimalCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
