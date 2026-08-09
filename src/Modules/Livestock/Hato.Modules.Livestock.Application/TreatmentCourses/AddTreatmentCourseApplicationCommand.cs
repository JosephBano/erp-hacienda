using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCourses;

/// <summary>
/// Appends one more day to an existing <see cref="Domain.TreatmentCourse"/> (task 5/8).
/// The dose is re-resolved against the subject's *current* weighing — a 3-day course
/// can span a day where the animal was weighed again — and the withdrawal period (if
/// the course carries withdrawal days) is recomputed from this new, later application,
/// per task 5's "no por aplicación suelta".
/// </summary>
public record AddTreatmentCourseApplicationCommand(
    Guid TreatmentCourseId,
    DateTimeOffset AppliedAt,
    decimal? AdministeredDoseAmount = null,
    string? AdministeredDoseUnit = null,
    string? Notes = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null) : IRequest<Guid>;

public class AddTreatmentCourseApplicationValidator : AbstractValidator<AddTreatmentCourseApplicationCommand>
{
    public AddTreatmentCourseApplicationValidator()
    {
        RuleFor(x => x.TreatmentCourseId).NotEmpty();
        RuleFor(x => x.AdministeredDoseUnit).MaximumLength(20).When(x => x.AdministeredDoseUnit is not null);
        RuleFor(x => x.MilkWithdrawalDays).GreaterThan(0).When(x => x.MilkWithdrawalDays.HasValue);
        RuleFor(x => x.MeatWithdrawalDays).GreaterThan(0).When(x => x.MeatWithdrawalDays.HasValue);
    }
}

public class AddTreatmentCourseApplicationHandler(ILivestockDbContext dbContext)
    : IRequestHandler<AddTreatmentCourseApplicationCommand, Guid>
{
    public async Task<Guid> Handle(AddTreatmentCourseApplicationCommand request, CancellationToken cancellationToken)
    {
        var course = await dbContext.TreatmentCourses
            .Include(c => c.Applications)
            .FirstOrDefaultAsync(c => c.Id == request.TreatmentCourseId, cancellationToken);

        if (course is null)
            throw new DomainException($"La serie de tratamiento con ID '{request.TreatmentCourseId}' no existe.");

        var doseKind = await dbContext.DoseKinds
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == course.DoseKindId, cancellationToken);
        if (doseKind is null)
            throw new DomainException($"La forma de dosis de la serie '{course.Id}' ya no existe.");

        var appliedAtUtc = request.AppliedAt.ToUniversalTime();

        var resolved = await DoseResolver.ResolveAsync(
            dbContext, doseKind.Key, course.DoseFactorAmount, course.DoseFactorUnit,
            course.AnimalId, course.GroupId, cancellationToken);

        var nextApplicationNo = course.Applications.Count == 0
            ? 1
            : course.Applications.Max(a => a.ApplicationNo) + 1;

        var application = course.AddApplication(
            applicationNo: nextApplicationNo,
            appliedAt: appliedAtUtc,
            calculatedDoseAmount: resolved.Amount,
            calculatedDoseUnit: resolved.Unit,
            isEstimated: resolved.IsEstimated,
            administeredDoseAmount: request.AdministeredDoseAmount,
            administeredDoseUnit: request.AdministeredDoseUnit,
            notes: CombineNotes(resolved.AutoNote, request.Notes));

        // The course itself is already tracked (loaded above), so EF's relationship
        // fixup alone leaves the new application's key-based state ambiguous — it
        // has a non-default Guid Id, so DetectChanges can read it as Modified
        // instead of Added. Adding it to its own DbSet explicitly (same pattern as
        // AddHealthPlanItemHandler) removes the ambiguity.
        dbContext.TreatmentCourseApplications.Add(application);

        TreatmentCourseWithdrawal.AddWithdrawalPeriods(
            dbContext, course.Id, course.AnimalId, appliedAtUtc,
            request.MilkWithdrawalDays, request.MeatWithdrawalDays);

        await dbContext.SaveChangesAsync(cancellationToken);

        return application.Id;
    }

    private static string? CombineNotes(string? autoNote, string? operatorNote)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(autoNote)) parts.Add(autoNote!.Trim());
        if (!string.IsNullOrWhiteSpace(operatorNote)) parts.Add(operatorNote!.Trim());
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}
