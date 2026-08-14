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
    Guid? BirthingId,
    decimal? BirthWeightKg = null);

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

    /// <summary>
    /// Counts how many of this litter's offspring already carry a Disposal event.
    /// Used by Breeding's cohort-weaning handler to compute the actual weaned count
    /// (PLAN-FASE-3-5-PORCINO.md sec.3.5a.4 task 5): a weaning that records BornAlive
    /// silently ignores the preweaning deaths that 3.5a.3 was built to capture.
    /// Animals that were tombstoned (mis-registration) are excluded: their absence is
    /// not a death.
    /// </summary>
    Task<int> CountPreweaningDeathsAsync(Guid birthingId, CancellationToken cancellationToken);
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
    /// joins an existing cohort or opens a new one.
    /// </summary>
    Task<SpeciesLactationProfile?> GetLactationProfileAsync(Guid speciesId, CancellationToken cancellationToken);
}

/// <summary>
/// Lightweight projection of an offspring row (sex, optional birth weight, optional
/// farm tag) for the read-side of a <c>recordBirth</c> flow. Carries no entity identity
/// beyond the animal id so the panel can render the "Detalle de crías" expansion
/// without coupling to <c>Livestock.Domain.Animal</c>.
/// </summary>
public record BirthingOffspringRow(
    Guid AnimalId,
    string Sex,
    decimal? BirthWeightKg,
    string? FarmTag);

/// <summary>
/// Read bundle for a single birthing: dam farm tag + the list of offspring rows.
/// The dam farm tag is empty-string-friendly (null when the animal has no active
/// FarmTag) so the caller can render either "—" or the actual tag.
/// </summary>
public record BirthingOffspringBundle(
    string? DamFarmTag,
    IReadOnlyList<BirthingOffspringRow> Offspring);

/// <summary>
/// Public read port used by Breeding to populate <c>BirthingListItemDto</c> with
/// the dam's farm tag and the offspring rows for the panel "Partos" tab, without
/// Breeding depending on <c>Livestock.Application.Abstractions</c> (Art. 6). The
/// caller passes the (birthingId, damId) pairs because the canonical source for
/// dam ids is <c>breeding.birthings.dam_id</c>, not <c>livestock.animals</c>.
/// </summary>
public interface IBirthingOffspringReader
{
    /// <summary>
    /// For each (birthingId, damId) pair, returns the dam's currently-active
    /// <see cref="IdentifierType.FarmTag"/> (null if absent) and the non-deleted
    /// offspring rows whose <c>BirthingId</c> matches. Birthings with no offspring
    /// yield an empty offspring list. Two SQL round-trips total no matter how many
    /// pairs are supplied.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, BirthingOffspringBundle>> GetForBirthingPairsAsync(
        IReadOnlyCollection<BirthingDamPair> pairs,
        CancellationToken cancellationToken);
}

/// <summary>
/// One row of the input to <see cref="IBirthingOffspringReader.GetForBirthingPairsAsync"/>:
/// which dam goes with which birthing. Lives in Contracts because the producer
/// (Breeding handler) and the consumer (the reader implementation) need to agree
/// on the shape; the data is already known on the producer side.
/// </summary>
public record BirthingDamPair(Guid BirthingId, Guid DamId);

/// <summary>
/// Lactation and cohort parameters of a species, as configured by the operator in the
/// panel. Backs the nursing cohort feature (3.5a.4): <c>DaysOfLactation</c> is added
/// to the cohort's latest birth to get the weaning date; <c>CohortWindowDays</c> is
/// the window during which a new birth joins the existing cohort instead of opening a new one.
/// </summary>
public record SpeciesLactationProfile(
    Guid SpeciesId,
    int? DaysOfLactation,
    int? CohortWindowDays);
