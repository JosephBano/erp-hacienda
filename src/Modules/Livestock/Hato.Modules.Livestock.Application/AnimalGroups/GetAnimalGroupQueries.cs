using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record GroupMembershipDto(Guid Id, Guid AnimalId, DateOnly JoinedAt, DateOnly? LeftAt, bool IsActive);

/// <summary>
/// Public projection of <see cref="AnimalGroup"/>. Extended in ADR-0025 with two
/// server-side derived fields (<c>LiveHeadCount</c>, <c>SpeciesName</c>) so the
/// admin-web list and detail views do not have to fan-out N requests.
/// </summary>
public record AnimalGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId,
    string? SpeciesName,
    bool IsActive,
    TrackingMode TrackingMode,
    int LiveHeadCount,
    List<GroupMembershipDto> Memberships);

public record GetAnimalGroupByIdQuery(Guid Id) : IRequest<AnimalGroupDto>;

public record GetAnimalGroupsQuery(bool IncludeInactive = false) : IRequest<List<AnimalGroupDto>>;

public class GetAnimalGroupByIdHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalGroupByIdQuery, AnimalGroupDto>
{
    public async Task<AnimalGroupDto> Handle(GetAnimalGroupByIdQuery request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .AsNoTracking()
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (group is null)
            throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        var liveHeadCount = await ComputeLiveHeadCountAsync(dbContext, group.Id, group.Memberships.Count(m => m.IsActive), cancellationToken);

        var speciesName = group.SpeciesId.HasValue
            ? await dbContext.Species
                .Where(s => s.Id == group.SpeciesId.Value)
                .Select(s => (string?)s.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            speciesName,
            group.IsActive,
            group.TrackingMode,
            liveHeadCount,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
    }

    // Internal so the list handler below can share it.
    internal static async Task<int> ComputeLiveHeadCountAsync(
        ILivestockDbContext dbContext, Guid groupId, int activeMemberships, CancellationToken cancellationToken)
    {
        var disposed = await dbContext.AnimalEvents
            .Where(e => e.GroupId == groupId && e.EventType == EventType.Disposal)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);
        return LiveHeadCountCalculator.Compute(activeMemberships, disposed);
    }
}

public class GetAnimalGroupsHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalGroupsQuery, List<AnimalGroupDto>>
{
    public async Task<List<AnimalGroupDto>> Handle(GetAnimalGroupsQuery request, CancellationToken cancellationToken)
    {
        // Query 1: groups + memberships (existing).
        var groupsQuery = dbContext.AnimalGroups
            .AsNoTracking()
            .Include(g => g.Memberships)
            .AsQueryable();
        if (!request.IncludeInactive)
            groupsQuery = groupsQuery.Where(g => g.IsActive);

        var groups = await groupsQuery.ToListAsync(cancellationToken);
        if (groups.Count == 0)
            return [];

        // Query 2: aggregate active memberships per group (one round-trip, no N+1).
        var groupIds = groups.Select(g => g.Id).ToList();
        var activeMembershipsByGroup = await dbContext.GroupMemberships
            .Where(m => groupIds.Contains(m.GroupId) && m.LeftAt == null)
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        // Query 3: aggregate disposals (Disposal events with AffectedCount) per group.
        var disposalsByGroup = await dbContext.AnimalEvents
            .Where(e => e.GroupId != null && groupIds.Contains(e.GroupId.Value) && e.EventType == EventType.Disposal)
            .GroupBy(e => e.GroupId!.Value)
            .Select(g => new { GroupId = g.Key, Disposed = g.Sum(e => e.AffectedCount ?? 0) })
            .ToDictionaryAsync(x => x.GroupId, x => x.Disposed, cancellationToken);

        // Query 4: species names for the species ids referenced by these groups.
        var speciesIds = groups.Where(g => g.SpeciesId.HasValue).Select(g => g.SpeciesId!.Value).Distinct().ToList();
        var speciesById = speciesIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Species
                .Where(s => speciesIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // Merge in memory; O(N) over the loaded groups.
        return groups.Select(group =>
        {
            var activeMembers = activeMembershipsByGroup.GetValueOrDefault(group.Id, 0);
            var disposed = disposalsByGroup.GetValueOrDefault(group.Id, 0);
            var liveHeadCount = LiveHeadCountCalculator.Compute(activeMembers, disposed);
            var speciesName = group.SpeciesId.HasValue && speciesById.TryGetValue(group.SpeciesId.Value, out var name) ? name : null;

            return new AnimalGroupDto(
                group.Id,
                group.Name,
                group.Description,
                group.SpeciesId,
                speciesName,
                group.IsActive,
                group.TrackingMode,
                liveHeadCount,
                group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
        }).ToList();
    }
}

/// <summary>
/// docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.7 task 6: the lot summary the field-app shows
/// when the operator taps on a headcount group. Combines the live head count with
/// the most recent group-subject event per kind (vaccination, diagnosis, disposal)
/// so the operator sees the state of the lot at a glance. The aggregate is
/// recomputed from <c>animal_events</c> on every read; never stored as an
/// editable counter (ADR-0015 sec.4).
/// </summary>
public record AnimalGroupSummaryDto(
    Guid GroupId,
    int LiveHeadCount,
    int HeadsAffectedByDiagnosis,
    DateTimeOffset? LastVaccinationAt,
    DateTimeOffset? LastDisposalAt,
    DateTimeOffset? LastTreatmentAt);

public record GetAnimalGroupSummaryQuery(Guid GroupId) : IRequest<AnimalGroupSummaryDto>;

public class GetAnimalGroupSummaryHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetAnimalGroupSummaryQuery, AnimalGroupSummaryDto>
{
    public async Task<AnimalGroupSummaryDto> Handle(
        GetAnimalGroupSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var groupExists = await dbContext.AnimalGroups
            .AnyAsync(g => g.Id == request.GroupId, cancellationToken);
        if (!groupExists)
        {
            // KeyNotFoundException is the contract ApiExceptionHandler maps to
            // 404 Not Found (DomainException maps to 400, which is wrong for a
            // read-side "does this exist?" lookup).
            throw new KeyNotFoundException($"El lote con ID '{request.GroupId}' no existe.");
        }

        // Live head count is the same number the GET /live-head-count endpoint
        // returns — capped at zero because a lot whose count is exhausted has
        // zero live heads, regardless of which exact sequence of disposals got
        // us there. (See fix on GetLiveHeadCountHandler.)
        // ADR-0025 sec.4: formula shared via LiveHeadCountCalculator so the list
        // and the summary cannot drift apart.
        var activeMemberships = await dbContext.GroupMemberships
            .CountAsync(m => m.GroupId == request.GroupId && m.LeftAt == null, cancellationToken);
        var disposed = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Disposal)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);
        var liveHeadCount = LiveHeadCountCalculator.Compute(activeMemberships, disposed);

        // GroupDiagnosis is the "hay N cabezas con X condición" event. We add up the
        // affected count across all open diagnoses — a chronically sick lot may have
        // several distinct diagnoses alive at the same time and each one tells the
        // operator a different thing.
        var headsAffectedByDiagnosis = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Diagnosis)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);

        var lastVaccinationAt = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Vaccination)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .MaxAsync(cancellationToken);

        var lastDisposalAt = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Disposal)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .MaxAsync(cancellationToken);

        var lastTreatmentAt = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Treatment)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .MaxAsync(cancellationToken);

        return new AnimalGroupSummaryDto(
            request.GroupId,
            liveHeadCount,
            headsAffectedByDiagnosis,
            lastVaccinationAt,
            lastDisposalAt,
            lastTreatmentAt);
    }
}
