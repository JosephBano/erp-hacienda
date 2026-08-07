using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.CrossModule;

/// <summary>
/// Implements Breeding's <see cref="IAnimalRegistrationService"/> contract: a newborn
/// inherits species/breed from its dam and is enrolled as a first-class Animal with
/// genealogy set, so "the calf was born inside the system" (Fase 2 exit criterion) holds.
/// </summary>
public class AnimalRegistrationService(ILivestockDbContext dbContext) : IAnimalRegistrationService
{
    public async Task<Guid> RegisterOffspringAsync(RegisterOffspringRequest request, CancellationToken cancellationToken)
    {
        var dam = await dbContext.Animals.FirstOrDefaultAsync(a => a.Id == request.DamId, cancellationToken)
            ?? throw new DomainException($"La madre con ID '{request.DamId}' no existe en Livestock.");

        var sex = ParseSex(request.Sex);

        var offspring = Animal.Register(
            dam.SpeciesId,
            sex,
            request.BirthDate,
            dam.BreedId,
            request.CategoryId);

        offspring.SetGenealogy(request.DamId, request.FatherAnimalId, request.FatherStrawId, request.BirthingId);

        if (!string.IsNullOrWhiteSpace(request.FarmTag))
            offspring.AssignIdentifier(IdentifierType.FarmTag, request.FarmTag, request.BirthDate);

        dbContext.Animals.Add(offspring);
        await dbContext.SaveChangesAsync(cancellationToken);

        return offspring.Id;
    }

    public async Task<Guid?> GetSpeciesAsync(Guid animalId, CancellationToken cancellationToken)
    {
        return await dbContext.Animals
            .AsNoTracking()
            .Where(a => a.Id == animalId && a.DeletedAt == null)
            .Select(a => (Guid?)a.SpeciesId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Sex ParseSex(string sex) => sex.Trim().ToUpperInvariant() switch
    {
        "M" or "MALE" or "MACHO" => Sex.Male,
        "F" or "FEMALE" or "HEMBRA" => Sex.Female,
        _ => Enum.Parse<Sex>(sex, ignoreCase: true),
    };
}
