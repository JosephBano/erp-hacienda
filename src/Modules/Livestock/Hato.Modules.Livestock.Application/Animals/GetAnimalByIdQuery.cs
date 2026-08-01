using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

public record GetAnimalByIdQuery(Guid AnimalId) : IRequest<AnimalDto?>;

public record AnimalIdentifierDto(IdentifierType Type, string Value, DateOnly ValidFrom, DateOnly? ValidTo);

public record AnimalDto(
    Guid Id,
    Guid SpeciesId,
    Guid? BreedId,
    Guid? CategoryId,
    Sex Sex,
    DateOnly? BirthDate,
    IReadOnlyCollection<AnimalIdentifierDto> Identifiers);

public class GetAnimalByIdHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalByIdQuery, AnimalDto?>
{
    public async Task<AnimalDto?> Handle(GetAnimalByIdQuery request, CancellationToken cancellationToken)
    {
        var animal = await dbContext.Animals
            .Include(a => a.Identifiers)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken);

        if (animal is null)
            return null;

        return new AnimalDto(
            animal.Id,
            animal.SpeciesId,
            animal.BreedId,
            animal.CategoryId,
            animal.Sex,
            animal.BirthDate,
            animal.Identifiers
                .Select(i => new AnimalIdentifierDto(i.Type, i.Value, i.ValidFrom, i.ValidTo))
                .ToList());
    }
}
