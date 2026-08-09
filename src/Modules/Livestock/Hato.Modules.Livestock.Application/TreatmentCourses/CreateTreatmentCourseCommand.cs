using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCourses;

/// <summary>
/// Starts a <see cref="TreatmentCourse"/> with its first application (task 5). A
/// single-application course is the common case (one vaccination, one shot); a
/// multi-day course grows via <see cref="AddTreatmentCourseApplicationCommand"/>.
/// </summary>
public record CreateTreatmentCourseCommand(
    Guid? AnimalId,
    Guid? GroupId,
    DateTimeOffset StartsAt,
    Guid RouteId,
    string? Reason,
    Guid? ProductId,
    Guid DoseKindId,
    decimal DoseFactorAmount,
    string DoseFactorUnit,
    string? Notes,
    // First application:
    decimal? AdministeredDoseAmount = null,
    string? AdministeredDoseUnit = null,
    string? ApplicationNotes = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null) : IRequest<Guid>;

public class CreateTreatmentCourseValidator : AbstractValidator<CreateTreatmentCourseCommand>
{
    public CreateTreatmentCourseValidator()
    {
        RuleFor(x => x)
            .Must(x => x.AnimalId.HasValue ^ x.GroupId.HasValue)
            .WithMessage("La serie de tratamiento debe estar asociada a exactamente un animal o un lote.");

        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.DoseKindId).NotEmpty();
        RuleFor(x => x.DoseFactorAmount).GreaterThan(0);
        RuleFor(x => x.DoseFactorUnit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Reason).MaximumLength(50).When(x => x.Reason is not null);
        RuleFor(x => x.AdministeredDoseUnit).MaximumLength(20).When(x => x.AdministeredDoseUnit is not null);
        RuleFor(x => x.MilkWithdrawalDays).GreaterThan(0).When(x => x.MilkWithdrawalDays.HasValue);
        RuleFor(x => x.MeatWithdrawalDays).GreaterThan(0).When(x => x.MeatWithdrawalDays.HasValue);
    }
}

public class CreateTreatmentCourseHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreateTreatmentCourseCommand, Guid>
{
    public async Task<Guid> Handle(CreateTreatmentCourseCommand request, CancellationToken cancellationToken)
    {
        var routeIsValid = await dbContext.AdministrationRoutes
            .AnyAsync(r => r.Id == request.RouteId && r.IsActive, cancellationToken);
        if (!routeIsValid)
            throw new DomainException($"La vía de administración con ID '{request.RouteId}' no existe o está inactiva.");

        var doseKind = await dbContext.DoseKinds
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == request.DoseKindId && k.IsActive, cancellationToken);
        if (doseKind is null)
            throw new DomainException($"La forma de dosis con ID '{request.DoseKindId}' no existe o está inactiva.");

        if (request.AnimalId is { } animalId)
        {
            var animalExists = await dbContext.Animals.AnyAsync(a => a.Id == animalId, cancellationToken);
            if (!animalExists)
                throw new DomainException($"El animal con ID '{animalId}' no existe.");
        }

        if (request.GroupId is { } groupId)
        {
            var groupExists = await dbContext.AnimalGroups.AnyAsync(g => g.Id == groupId, cancellationToken);
            if (!groupExists)
                throw new DomainException($"El lote con ID '{groupId}' no existe.");
        }

        var startsAtUtc = request.StartsAt.ToUniversalTime();

        var course = request.AnimalId is { } animal
            ? TreatmentCourse.CreateForAnimal(
                animal, startsAtUtc, request.RouteId, request.Reason, request.ProductId,
                request.DoseKindId, request.DoseFactorAmount, request.DoseFactorUnit, request.Notes)
            : TreatmentCourse.CreateForGroup(
                request.GroupId!.Value, startsAtUtc, request.RouteId, request.Reason, request.ProductId,
                request.DoseKindId, request.DoseFactorAmount, request.DoseFactorUnit, request.Notes);

        var resolved = await DoseResolver.ResolveAsync(
            dbContext, doseKind.Key, request.DoseFactorAmount, request.DoseFactorUnit,
            request.AnimalId, request.GroupId, cancellationToken);

        course.AddApplication(
            applicationNo: 1,
            appliedAt: startsAtUtc,
            calculatedDoseAmount: resolved.Amount,
            calculatedDoseUnit: resolved.Unit,
            isEstimated: resolved.IsEstimated,
            administeredDoseAmount: request.AdministeredDoseAmount,
            administeredDoseUnit: request.AdministeredDoseUnit,
            notes: CombineNotes(resolved.AutoNote, request.ApplicationNotes));

        dbContext.TreatmentCourses.Add(course);

        TreatmentCourseWithdrawal.AddWithdrawalPeriods(
            dbContext, course.Id, request.AnimalId, startsAtUtc,
            request.MilkWithdrawalDays, request.MeatWithdrawalDays);

        await dbContext.SaveChangesAsync(cancellationToken);

        return course.Id;
    }

    private static string? CombineNotes(string? autoNote, string? operatorNote)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(autoNote)) parts.Add(autoNote!.Trim());
        if (!string.IsNullOrWhiteSpace(operatorNote)) parts.Add(operatorNote!.Trim());
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}
