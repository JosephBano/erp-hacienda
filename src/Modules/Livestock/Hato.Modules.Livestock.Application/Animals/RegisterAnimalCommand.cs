using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;

namespace Hato.Modules.Livestock.Application.Animals;

public record RegisterAnimalCommand(
    Guid SpeciesId, Sex Sex, DateOnly? BirthDate, Guid? BreedId, Guid? CategoryId) : IRequest<Guid>;

public class RegisterAnimalHandler(ILivestockDbContext dbContext) : IRequestHandler<RegisterAnimalCommand, Guid>
{
    public async Task<Guid> Handle(RegisterAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = Animal.Register(
            request.SpeciesId, request.Sex, request.BirthDate, request.BreedId, request.CategoryId);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return animal.Id;
    }
}
