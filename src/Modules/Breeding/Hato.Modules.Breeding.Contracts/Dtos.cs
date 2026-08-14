namespace Hato.Modules.Breeding.Contracts;

public record SemenStrawDto(
    Guid Id,
    string Code,
    string BullName,
    string? BullCode,
    Guid BreedId,
    string? SupplierName,
    int InitialQuantity,
    int CurrentQuantity,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record BreedingServiceDto(
    Guid Id,
    Guid DamId,
    string ServiceType,
    Guid? SireAnimalId,
    Guid? StrawId,
    DateOnly ServiceDate,
    string? Technician,
    string? Notes,
    decimal? BodyConditionScore,
    DateTimeOffset CreatedAt
);

public record PregnancyCheckDto(
    Guid Id,
    Guid ServiceId,
    Guid DamId,
    DateOnly CheckDate,
    string Method,
    string Result,
    string? CheckedBy,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record PregnancyDto(
    Guid Id,
    Guid DamId,
    Guid? ServiceId,
    DateOnly ConfirmedAt,
    DateOnly ExpectedBirthDate,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record BirthingDto(
    Guid Id,
    Guid DamId,
    Guid? PregnancyId,
    DateOnly BirthDate,
    string Difficulty,
    int TotalBorn,
    int BornAlive,
    int BornDead,
    int Mummified,
    decimal? LitterWeight,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateOnly? WeanedAt = null,
    int? WeanedCount = null,
    Guid? NursingCohortId = null
);

/// <summary>
/// Read-side projection of a birthing for the panel "Partos" list. Includes the dam's
/// farm tag and the offspring rows (animal id, sex, optional birth weight) so the
/// panel can show the user what they just registered — the birth weight in
/// particular is the data point that motivated this DTO (PLAN-FASE-3-5-PORCINO.md
/// sec.3.5a.4 task 3). Separate from <see cref="BirthingDto"/> so write-side and
/// list-side contracts can evolve independently.
/// </summary>
public record BirthingListItemDto(
    Guid Id,
    Guid DamId,
    string? DamFarmTag,
    DateOnly BirthDate,
    string Difficulty,
    int TotalBorn,
    int BornAlive,
    int BornDead,
    int Mummified,
    decimal? LitterWeight,
    string? Notes,
    Guid? NursingCohortId,
    DateOnly? WeanedAt,
    int? WeanedCount,
    List<BirthingListOffspringDto> Offspring);

public record BirthingListOffspringDto(
    Guid AnimalId,
    string? FarmTag,
    string Sex,
    decimal? BirthWeightKg);

public record NursingCohortDto(
    Guid Id,
    Guid SpeciesId,
    DateOnly StartedAt,
    DateOnly? ClosedAt,
    DateOnly? WeanedAt,
    int LitterCount,
    int TotalBornAlive,
    string? Notes,
    DateTimeOffset CreatedAt);

public record AncestorDto(
    Guid AnimalId,
    string? FarmTag,
    string Sex,
    int GenerationLevel,
    string Role, // "Mother", "Father"
    Guid? MotherId,
    Guid? FatherAnimalId,
    Guid? FatherStrawId,
    string? FatherStrawBullName
);

public record PedigreeDto(
    Guid AnimalId,
    string? FarmTag,
    List<AncestorDto> Ancestors
);
