using Hato.Modules.Tasks.Application.Abstractions;
using Hato.Modules.Tasks.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Tasks.Application.Alerts;

public record GetActiveAlertsQuery() : IRequest<List<AlertDto>>;

public class GetActiveAlertsQueryHandler(ITasksDbContext dbContext)
    : IRequestHandler<GetActiveAlertsQuery, List<AlertDto>>
{
    public async Task<List<AlertDto>> Handle(GetActiveAlertsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Alerts
            .AsNoTracking()
            .Where(a => !a.IsDismissed)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AlertDto(
                a.Id,
                a.Code,
                a.Title,
                a.Message,
                a.Severity.ToString(),
                a.TargetEntityId,
                a.IsDismissed,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
