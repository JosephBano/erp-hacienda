using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;

namespace Hato.Modules.Livestock.Application.Breeds;

public record CreateBreedCommand(Guid SpeciesId, string Name) : IRequest<Guid>;

public class CreateBreedHandler(ILivestockDbContext dbContext) : IRequestHandler<CreateBreedCommand, Guid>
{
    public async Task<Guid> Handle(CreateBreedCommand request, CancellationToken cancellationToken)
    {
        var breed = Hato.Modules.Livestock.Domain.Breed.Create(request.SpeciesId, request.Name);

        dbContext.Breeds.Add(breed);
        await dbContext.SaveChangesAsync(cancellationToken);

        return breed.Id;
    }
}
