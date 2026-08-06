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
