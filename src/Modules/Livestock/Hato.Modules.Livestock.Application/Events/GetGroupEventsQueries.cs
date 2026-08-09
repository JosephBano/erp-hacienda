using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Events;

public record GetGroupEventsQuery(Guid GroupId) : IRequest<List<AnimalEventDto>>;

public class GetGroupEventsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetGroupEventsQuery, List<AnimalEventDto>>
{
    public async Task<List<AnimalEventDto>> Handle(GetGroupEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await dbContext.AnimalEvents
            .AsNoTracking()
            .Where(e => e.GroupId == request.GroupId)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        return events.Select(e => new AnimalEventDto(
            e.Id, e.AnimalId, e.GroupId, e.EventType, e.OccurredAt, e.RecordedBy,
            e.RecordedById, e.Cost, e.PayloadJson, e.RelatedEventId, e.AffectedCount, e.CauseId,
            e.RouteId, e.Reason, e.BatchId, e.HealthPlanItemId, e.AppliedByUserId
        )).ToList();
    }
}

/// <summary>
/// The single reading of ADR-0015 sec.4: active memberships minus disposals recorded
/// against the lot. Never a stored, editable counter — always recomputed from the events
/// that actually happened. Capped at zero: a lot whose members were all disposed
/// (whether by a single cascade-closing disposal or by a chain of partial ones whose
/// sums happened to reach the membership count) reports <c>0</c>, not a negative
/// number — the aggregate of live heads is a count, never a debt.
/// </summary>
public record GetLiveHeadCountQuery(Guid GroupId) : IRequest<int>;

public class GetLiveHeadCountHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetLiveHeadCountQuery, int>
{
    public async Task<int> Handle(GetLiveHeadCountQuery request, CancellationToken cancellationToken)
    {
        var groupExists = await dbContext.AnimalGroups.AnyAsync(g => g.Id == request.GroupId, cancellationToken);
        if (!groupExists)
            throw new DomainException($"El lote con ID '{request.GroupId}' no existe.");

        var activeMemberships = await dbContext.GroupMemberships
            .CountAsync(m => m.GroupId == request.GroupId && m.LeftAt == null, cancellationToken);

        var disposed = await dbContext.AnimalEvents
            .Where(e => e.GroupId == request.GroupId && e.EventType == EventType.Disposal)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);

        // Bug found in the Fase 3.5 retrospective: when a cascade closure runs, every
        // active membership goes to LeftAt != null on the same write, and the very next
        // read of this handler sees activeMemberships = 0 while disposed is the full
        // historical sum. The subtraction returned a negative number that propagated
        // through every aggregate that asks "how many live heads has the farm?"
        // (e.g. the post-#54 run #54 CI: Expected: 0 / Actual: -6 and -20). The cap is
        // not a patch over an inconsistency — it is the correct semantic: a lot whose
        // count is exhausted has zero live heads, regardless of which exact sequence of
        // partial disposals got us there.
        return Math.Max(0, activeMemberships - disposed);
    }
}
