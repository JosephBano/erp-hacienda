using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Livestock.Contracts;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Cohorts;

/// <summary>
/// Classifies a weaned nursing cohort by weight into N headcount engorde lots
/// (3.5a.4 task 4, ADR-0023). The operation is the canonical way the client
/// transitions from "individual" to "headcount" tracking: from this moment on, the
/// animals are members of <c>AnimalGroup</c>s in <c>TrackingMode.Headcount</c> and
/// <c>ResolveIndividualState</c> returns <c>Indeterminate</c> for them.
///
/// Emission pattern (ADR-0023 sec.3):
/// <list type="bullet">
/// <item>One <c>WeightSorted</c> event per offspring (last individual footprint) carrying
///       the target group id and the weight measured.</item>
/// <item>One <c>GroupWeightSorting</c> event per target group (group footprint) with
///       the count, the average/min/max weight, and the source cohort id.</item>
/// <item>One <c>GroupMembership</c> row per offspring (move from "in a nursing cohort"
///       to "in an engorde lot") persisted in the same transaction as the events.</item>
/// </list>
///
/// The total number of rows of event is therefore <c>N + M</c> (offspring + groups),
/// not <c>2N</c> nor <c>2M</c>. The variance is what the consumer reads.
/// </summary>
public record ClassifyCohortByWeightCommand(
    Guid NursingCohortId,
    DateOnly SortingDate,
    List<WeightSortingAssignment> Assignments,
    string? Notes = null) : IRequest<CohortClassificationResult>;

public record WeightSortingAssignment(
    Guid AnimalId,
    Guid TargetGroupId,
    decimal WeightKg);

public record CohortClassificationResult(
    Guid NursingCohortId,
    DateOnly SortedAt,
    int TotalHeadSorted,
    List<SortedGroupSummary> Groups);

public record SortedGroupSummary(
    Guid GroupId,
    int HeadCount,
    decimal AvgWeightKg,
    decimal MinWeightKg,
    decimal MaxWeightKg);

public class ClassifyCohortByWeightValidator : AbstractValidator<ClassifyCohortByWeightCommand>
{
    public ClassifyCohortByWeightValidator()
    {
        RuleFor(x => x.NursingCohortId).NotEmpty();
        RuleFor(x => x.Assignments).NotEmpty();
    }
}

public class ClassifyCohortByWeightCommandHandler(
    IBreedingDbContext dbContext,
    IAnimalGroupWriter groupWriter) : IRequestHandler<ClassifyCohortByWeightCommand, CohortClassificationResult>
{
    public async Task<CohortClassificationResult> Handle(
        ClassifyCohortByWeightCommand request,
        CancellationToken cancellationToken)
    {
        var cohort = await dbContext.NursingCohorts
            .FirstOrDefaultAsync(c => c.Id == request.NursingCohortId, cancellationToken)
            ?? throw new DomainException($"La cohorte '{request.NursingCohortId}' no existe.");

        var birthings = await dbContext.Birthings
            .Where(b => b.NursingCohortId == cohort.Id)
            .ToListAsync(cancellationToken);

        if (birthings.Count == 0)
            throw new DomainException(
                "La cohorte no tiene camadas registradas; no se puede clasificar una cohorte vacía.");

        // Validate the cohort's lifecycle state.
        if (cohort.WeanedAt is null)
            throw new DomainException(
                "La cohorte aún no ha sido destetada; no se puede clasificar por peso.");

        if (cohort.SortedAt is not null)
            throw new DomainException(
                $"La cohorte ya fue clasificada por peso el {cohort.SortedAt:yyyy-MM-dd}.");

        // Compute the cohort's total weaned count.
        var totalWeaned = birthings.Sum(b => b.WeanedCount);
        if (totalWeaned <= 0)
            throw new DomainException(
                "La cohorte no tiene crías destetadas vivas; no se puede clasificar.");

        // Validate the assignments: count, animals, target groups.
        var assignmentCount = request.Assignments.Count;
        if (assignmentCount != totalWeaned)
            throw new DomainException(
                $"La suma de cabezas asignadas ({assignmentCount}) no coincide con las cabezas " +
                $"destetadas de la cohorte ({totalWeaned}).");

        var distinctAnimalIds = request.Assignments.Select(a => a.AnimalId).Distinct().ToList();
        if (distinctAnimalIds.Count != request.Assignments.Count)
            throw new DomainException(
                "Un animal no puede aparecer dos veces en la lista de clasificaciones.");

        // Validate weights are positive.
        var invalidWeights = request.Assignments.Where(a => a.WeightKg <= 0).ToList();
        if (invalidWeights.Count > 0)
            throw new DomainException(
                $"Los siguientes pesos no son válidos (deben ser positivos): " +
                $"{string.Join(", ", invalidWeights.Select(a => a.AnimalId))}.");

        // Emit the events and the memberships via the cross-module port
        // (IAnimalGroupWriter). The implementation is in the Livestock
        // infrastructure layer; this handler stays domain-pure.
        var sortedAt = new DateTimeOffset(
            request.SortingDate.Year,
            request.SortingDate.Month,
            request.SortingDate.Day,
            12, 0, 0, TimeSpan.Zero);

        var groupFootprints = await groupWriter.EmitGroupWeightSortingAsync(
            cohort.Id,
            sortedAt,
            request.Assignments
                .Select(a => new WeightSortingAssignmentDto(a.AnimalId, a.TargetGroupId, a.WeightKg))
                .ToList(),
            recordedBy: "weight-classification",
            cancellationToken);

        // Mark the cohort as sorted.
        cohort.MarkSorted(request.SortingDate, request.Notes);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CohortClassificationResult(
            cohort.Id,
            request.SortingDate,
            assignmentCount,
            groupFootprints
                .Select(g => new SortedGroupSummary(g.GroupId, g.HeadCount, g.AvgWeightKg, g.MinWeightKg, g.MaxWeightKg))
                .ToList());
    }
}
