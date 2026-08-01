using Hato.Modules.Tasks.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Tasks.Application.Alerts;

public record DismissAlertCommand(Guid AlertId, Guid? UserId = null) : IRequest<bool>;

public class DismissAlertCommandHandler(ITasksDbContext dbContext)
    : IRequestHandler<DismissAlertCommand, bool>
{
    public async Task<bool> Handle(DismissAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken);
        if (alert is null)
            return false;

        alert.Dismiss(request.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
