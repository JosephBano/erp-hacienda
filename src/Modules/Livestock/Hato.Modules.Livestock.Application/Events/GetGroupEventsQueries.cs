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
            e.Cost, e.PayloadJson, e.RelatedEventId, e.AffectedCount
        )).ToList();
    }
}

/// <summary>
/// The single reading of ADR-0015 sec.4: active memberships minus disposals recorded
/// against the lot. Never a stored, editable counter — always recomputed from the events
/// that actually happened.
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

        return activeMemberships - disposed;
    }
}
