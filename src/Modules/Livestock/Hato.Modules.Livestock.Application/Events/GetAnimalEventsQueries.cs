using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Events;

public record AnimalEventDto(
    Guid Id,
    Guid? AnimalId,
    Guid? GroupId,
    EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    decimal? Cost,
    string PayloadJson,
    Guid? RelatedEventId,
    int? AffectedCount,
    Guid? CauseId);

public record WithdrawalPeriodDto(
    Guid Id,
    Guid AnimalId,
    Guid EventId,
    WithdrawalTarget Target,
    DateOnly StartsAt,
    DateOnly EndsAt,
    bool IsActive);

public record GetAnimalEventsQuery(Guid AnimalId) : IRequest<List<AnimalEventDto>>;

public record GetActiveWithdrawalsQuery(Guid AnimalId, DateOnly TargetDate) : IRequest<List<WithdrawalPeriodDto>>;

public class GetAnimalEventsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetAnimalEventsQuery, List<AnimalEventDto>>
{
    public async Task<List<AnimalEventDto>> Handle(GetAnimalEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await dbContext.AnimalEvents
            .AsNoTracking()
            .Where(e => e.AnimalId == request.AnimalId)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        return events.Select(e => new AnimalEventDto(
            e.Id,
            e.AnimalId,
            e.GroupId,
            e.EventType,
            e.OccurredAt,
            e.RecordedBy,
            e.Cost,
            e.PayloadJson,
            e.RelatedEventId,
            e.AffectedCount,
            e.CauseId
        )).ToList();
    }
}

public class GetActiveWithdrawalsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetActiveWithdrawalsQuery, List<WithdrawalPeriodDto>>
{
    public async Task<List<WithdrawalPeriodDto>> Handle(GetActiveWithdrawalsQuery request, CancellationToken cancellationToken)
    {
        var withdrawals = await dbContext.WithdrawalPeriods
            .AsNoTracking()
            .Where(w => w.AnimalId == request.AnimalId && w.StartsAt <= request.TargetDate && w.EndsAt >= request.TargetDate)
            .ToListAsync(cancellationToken);

        return withdrawals.Select(w => new WithdrawalPeriodDto(
            w.Id,
            w.AnimalId,
            w.EventId,
            w.Target,
            w.StartsAt,
            w.EndsAt,
            w.IsActiveOn(request.TargetDate)
        )).ToList();
    }
}
