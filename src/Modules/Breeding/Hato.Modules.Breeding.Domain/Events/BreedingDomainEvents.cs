using Hato.SharedKernel;
using Hato.Modules.Breeding.Domain.Enums;

namespace Hato.Modules.Breeding.Domain.Events;

public record BreedingServiceRegisteredEvent(
    Guid ServiceId,
    Guid DamId,
    ServiceType ServiceType,
    Guid? SireAnimalId,
    Guid? StrawId,
    DateOnly ServiceDate,
    DateTimeOffset OccurredAt = default
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt == default ? DateTimeOffset.UtcNow : OccurredAt;
}

public record PregnancyCheckRecordedEvent(
    Guid CheckId,
    Guid ServiceId,
    Guid DamId,
    CheckResult Result,
    DateOnly CheckDate,
    DateTimeOffset OccurredAt = default
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt == default ? DateTimeOffset.UtcNow : OccurredAt;
}

public record PregnancyConfirmedEvent(
    Guid PregnancyId,
    Guid DamId,
    Guid? ServiceId,
    DateOnly ExpectedBirthDate,
    DateTimeOffset OccurredAt = default
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt == default ? DateTimeOffset.UtcNow : OccurredAt;
}

public record OffspringBirthInfo(
    Guid ChildId,
    string? FarmTag,
    string Sex, // "M" or "F"
    decimal? BirthWeightKg,
    Guid CategoryId
);

public record BirthingRecordedEvent(
    Guid BirthingId,
    Guid DamId,
    Guid? PregnancyId,
    DateOnly BirthDate,
    Guid? SireAnimalId,
    Guid? FatherStrawId,
    List<OffspringBirthInfo> Offspring,
    DateTimeOffset OccurredAt = default
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = OccurredAt == default ? DateTimeOffset.UtcNow : OccurredAt;
}
