using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record GroupMembershipDto(Guid Id, Guid AnimalId, DateOnly JoinedAt, DateOnly? LeftAt, bool IsActive);

public record AnimalGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId,
    bool IsActive,
    TrackingMode TrackingMode,
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
            throw new DomainException($"El grupo con ID '{request.Id}' no existe.");

        return new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            group.IsActive,
            group.TrackingMode,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
    }
}

public class GetAnimalGroupsHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalGroupsQuery, List<AnimalGroupDto>>
{
    public async Task<List<AnimalGroupDto>> Handle(GetAnimalGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.AnimalGroups
            .AsNoTracking()
            .Include(g => g.Memberships)
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(g => g.IsActive);

        var groups = await query.ToListAsync(cancellationToken);

        return groups.Select(group => new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            group.IsActive,
            group.TrackingMode,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList()
        )).ToList();
    }
}

/// <summary>
/// PLAN-FASE-3-5-PORCINO.md sec.3.5a.7 task 6: the lot summary the field-app shows
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
        var activeMemberships = await dbContext.GroupMemberships
            .CountAsync(m => m.GroupId == request.GroupId && m.LeftAt == null, cancellationToken);
        var disposed = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Disposal)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);
        var liveHeadCount = Math.Max(0, activeMemberships - disposed);

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
