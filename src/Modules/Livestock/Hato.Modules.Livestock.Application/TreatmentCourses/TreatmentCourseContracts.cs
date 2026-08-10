namespace Hato.Modules.Livestock.Application.TreatmentCourses;

public record TreatmentCourseApplicationDto(
    Guid Id,
    int ApplicationNo,
    DateTimeOffset AppliedAt,
    decimal? CalculatedDoseAmount,
    string? CalculatedDoseUnit,
    bool IsEstimated,
    decimal? AdministeredDoseAmount,
    string? AdministeredDoseUnit,
    string? Notes,
    bool IsPlausibilityConfirmed);

public record TreatmentCourseDto(
    Guid Id,
    Guid? AnimalId,
    Guid? GroupId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    Guid RouteId,
    string? Reason,
    Guid? ProductId,
    Guid DoseKindId,
    decimal DoseFactorAmount,
    string DoseFactorUnit,
    string? Notes,
    bool IsSynthetic,
    List<TreatmentCourseApplicationDto> Applications);
