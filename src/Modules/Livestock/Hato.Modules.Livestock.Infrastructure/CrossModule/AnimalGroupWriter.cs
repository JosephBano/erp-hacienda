using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

/// <summary>
/// Implementation of <see cref="IAnimalGroupWriter"/> for the cohort
/// classification (3.5a.4 task 4, ADR-0023). Emits the per-animal
/// <c>WeightSorted</c> events, the per-group <c>GroupWeightSorting</c> events, and
/// the <c>GroupMembership</c> rows that move the weaned offspring into their
/// destination headcount engorde lots. All three are persisted in the same
/// transaction as the cohort's <c>MarkSorted</c> invocation — the caller
/// controls the transaction boundary through its own <c>DbContext.SaveChanges</c>.
/// </summary>
public class AnimalGroupWriter(ILivestockDbContext dbContext) : IAnimalGroupWriter
{
    public async Task<List<AnimalGroupWeightSortingSummary>> EmitGroupWeightSortingAsync(
        Guid sourceNursingCohortId,
        DateTimeOffset sortedAt,
        List<WeightSortingAssignmentDto> assignments,
        string recordedBy,
        CancellationToken cancellationToken)
    {
        // ---- Validate the assignments against the live data. ------------
        var animalIds = assignments.Select(a => a.AnimalId).Distinct().ToList();
        var targetGroupIds = assignments.Select(a => a.TargetGroupId).Distinct().ToList();

        var groups = await dbContext.AnimalGroups
            .Where(g => targetGroupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        var missingGroups = targetGroupIds.Where(id => !groups.Any(g => g.Id == id)).ToList();
        if (missingGroups.Count > 0)
            throw new DomainException(
                $"Los siguientes grupos no existen: {string.Join(", ", missingGroups)}.");

        var wrongKindGroups = groups
            .Where(g => g.TrackingMode != TrackingMode.Headcount)
            .Select(g => g.Id)
            .ToList();
        if (wrongKindGroups.Count > 0)
            throw new DomainException(
                $"Los siguientes grupos deben estar en modo Headcount: " +
                $"{string.Join(", ", wrongKindGroups)}.");

        var inactiveGroups = groups.Where(g => !g.IsActive).Select(g => g.Id).ToList();
        if (inactiveGroups.Count > 0)
            throw new DomainException(
                $"Los siguientes grupos están inactivos: {string.Join(", ", inactiveGroups)}.");

        // ---- Per-animal WeightSorted events (last individual footprint). ---
        var weightSortedEvents = new List<AnimalEvent>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var ev = AnimalEvent.Create(
                animalId: assignment.AnimalId,
                eventType: EventType.WeightSorted,
                occurredAt: sortedAt,
                recordedBy: recordedBy,
                payloadJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    target_group_id = assignment.TargetGroupId,
                    weight_kg = assignment.WeightKg,
                    source_nursing_cohort_id = sourceNursingCohortId,
                }));

            weightSortedEvents.Add(ev);
        }
        await dbContext.AnimalEvents.AddRangeAsync(weightSortedEvents, cancellationToken);

        // ---- Per-target-group GroupWeightSorting events + memberships. ----
        var summaries = new List<AnimalGroupWeightSortingSummary>();
        var joinedAt = DateOnly.FromDateTime(sortedAt.UtcDateTime);

        foreach (var group in groups)
        {
            var groupAssignments = assignments.Where(a => a.TargetGroupId == group.Id).ToList();
            if (groupAssignments.Count == 0)
                continue;

            var weights = groupAssignments.Select(a => a.WeightKg).ToList();
            var avg = weights.Sum() / weights.Count;
            var min = weights.Min();
            var max = weights.Max();

            var groupEvent = AnimalEvent.CreateForGroup(
                groupId: group.Id,
                eventType: EventType.GroupWeightSorting,
                occurredAt: sortedAt,
                recordedBy: recordedBy,
                affectedCount: groupAssignments.Count,
                payloadJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    source_nursing_cohort_id = sourceNursingCohortId,
                    head_count = groupAssignments.Count,
                    avg_weight_kg = avg,
                    min_weight_kg = min,
                    max_weight_kg = max,
                }));

            await dbContext.AnimalEvents.AddAsync(groupEvent, cancellationToken);

            // Memberships: move the offspring to their destination lot.
            foreach (var assignment in groupAssignments)
            {
                var membership = GroupMembership.Create(assignment.AnimalId, group.Id, joinedAt);
                await dbContext.GroupMemberships.AddAsync(membership, cancellationToken);
            }

            summaries.Add(new AnimalGroupWeightSortingSummary(group.Id, groupAssignments.Count, avg, min, max));
        }

        return summaries;
    }
}
