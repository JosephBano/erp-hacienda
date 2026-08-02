namespace Hato.Modules.Livestock.Contracts;

public record AnimalAncestorDto(
    Guid AnimalId,
    string? FarmTag,
    string Sex,
    int GenerationLevel,
    string Role, // "Self", "Mother", "Father"
    Guid? MotherId,
    Guid? FatherAnimalId,
    Guid? FatherStrawId);

/// <summary>Public read port for Breeding's pedigree feature (Art. 6).</summary>
public interface IAnimalGenealogyReader
{
    Task<IReadOnlyList<AnimalAncestorDto>> GetAncestryAsync(Guid animalId, int maxGenerations, CancellationToken cancellationToken);
}

public record RegisterOffspringRequest(
    Guid DamId,
    string Sex,
    DateOnly BirthDate,
    Guid? CategoryId,
    string? FarmTag,
    Guid? FatherAnimalId,
    Guid? FatherStrawId,
    Guid? BirthingId);

/// <summary>
/// Public write port used by Breeding to enroll a newborn as a first-class Animal with
/// genealogy set, without Breeding depending on Livestock.Domain (Art. 6).
/// </summary>
public interface IAnimalRegistrationService
{
    Task<Guid> RegisterOffspringAsync(RegisterOffspringRequest request, CancellationToken cancellationToken);
}
