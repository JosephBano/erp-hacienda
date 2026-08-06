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

    /// <summary>
    /// Reads the species id of an animal. Used by Breeding to pick the right cohort
    /// window when a birthing is recorded (PLAN-FASE-3-5-PORCINO.md sec.3.5a.4). Returns
    /// null when the animal does not exist or has been soft-deleted, so the calling
    /// command can fall back to "no cohort" without an exception.
    /// </summary>
    Task<Guid?> GetSpeciesAsync(Guid animalId, CancellationToken cancellationToken);
}

/// <summary>
/// Public read port for Breeding to resolve species-specific gestation length (bovine
/// ~283 days, porcine ~114 days — a DB-configured parameter per Art. 8, never a constant
/// or an if/switch in domain code).
/// </summary>
public interface IAnimalSpeciesReader
{
    /// <summary>Null if the animal doesn't exist or its species has no configured gestation length.</summary>
    Task<int?> GetGestationDaysAsync(Guid animalId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the lactation parameters of a species directly (PLAN-FASE-3-5-PORCINO.md
    /// sec.3.5a.4). Returns nulls when the species has not been configured yet, so the
    /// application layer can refuse cohort weaning with a clear message instead of
    /// silently defaulting to 24. The cohort window is the only addition relative to
    /// <see cref="GetGestationDaysAsync"/>; it is what decides whether a new birthing
    /// joins an open cohort or opens its own.
    /// </summary>
    Task<SpeciesLactationProfile?> GetLactationProfileAsync(Guid speciesId, CancellationToken cancellationToken);
}

/// <summary>
/// Lactation and cohort parameters for a species, as configured by the operator in the
/// panel. Backs the nursing cohort feature (3.5a.4): <c>DaysOfLactation</c> is added to
/// the cohort's latest birth to get the weaning date; <c>CohortWindowDays</c> is the
/// window during which a new birth joins the existing cohort instead of opening a new one.
/// </summary>
public record SpeciesLactationProfile(
    Guid SpeciesId,
    int? DaysOfLactation,
    int? CohortWindowDays);
