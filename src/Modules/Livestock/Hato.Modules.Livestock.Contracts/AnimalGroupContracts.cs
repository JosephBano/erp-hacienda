namespace Hato.Modules.Livestock.Contracts;

/// <summary>
/// Public write port used by Breeding to emit the group-side events and
/// memberships of a cohort classification (3.5a.4 task 4, ADR-0023). The port
/// keeps Breeding free of any reference to Livestock's domain types
/// (Art. 6: modules communicate by contracts, not by direct table access).
/// </summary>
public interface IAnimalGroupWriter
{
    /// <summary>
    /// Emits one <c>GroupWeightSorting</c> event per destination group, with
    /// the head count and the weight summary (avg/min/max). The caller
    /// persists the membership rows in the same transaction.
    /// </summary>
    /// <returns>
    /// One summary per distinct <c>TargetGroupId</c> in the assignments, in
    /// the order they were first encountered.
    /// </returns>
    Task<List<AnimalGroupWeightSortingSummary>> EmitGroupWeightSortingAsync(
        Guid sourceNursingCohortId,
        DateTimeOffset sortedAt,
        List<WeightSortingAssignmentDto> assignments,
        string recordedBy,
        CancellationToken cancellationToken);
}

/// <summary>
/// Wire-format DTO for a single cohort-classification assignment
/// (Animal → Headcount group, with weight in kg). Used by the
/// <see cref="IAnimalGroupWriter"/> port.
/// </summary>
public record WeightSortingAssignmentDto(
    Guid AnimalId,
    Guid TargetGroupId,
    decimal WeightKg);

/// <summary>
/// Wire-format DTO returned by <see cref="IAnimalGroupWriter.EmitGroupWeightSortingAsync"/>.
/// </summary>
public record AnimalGroupWeightSortingSummary(
    Guid GroupId,
    int HeadCount,
    decimal AvgWeightKg,
    decimal MinWeightKg,
    decimal MaxWeightKg);
