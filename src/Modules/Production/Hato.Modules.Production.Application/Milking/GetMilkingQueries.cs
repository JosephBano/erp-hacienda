using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Application.Milking;

public record MilkYieldDto(Guid Id, Guid AnimalId, decimal Liters);

public record MilkingSessionDto(
    Guid Id,
    DateOnly Date,
    MilkingShift Shift,
    Guid? GroupId,
    decimal TotalLiters,
    string RecordedBy,
    string? Notes,
    List<MilkYieldDto> Yields,
    bool IsPlausibilityConfirmed = false);

public record GetDailyMilkingSessionsQuery(DateOnly Date) : IRequest<List<MilkingSessionDto>>;

public class GetDailyMilkingSessionsHandler(IProductionDbContext dbContext)
    : IRequestHandler<GetDailyMilkingSessionsQuery, List<MilkingSessionDto>>
{
    public async Task<List<MilkingSessionDto>> Handle(GetDailyMilkingSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.MilkingSessions
            .AsNoTracking()
            .Include(s => s.Yields)
            .Where(s => s.Date == request.Date)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new MilkingSessionDto(
            s.Id,
            s.Date,
            s.Shift,
            s.GroupId,
            s.TotalLiters,
            s.RecordedBy,
            s.Notes,
            s.Yields.Select(y => new MilkYieldDto(y.Id, y.AnimalId, y.Liters)).ToList(),
            s.IsPlausibilityConfirmed
        )).ToList();
    }
}
