using Hato.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Tasks.Application.Abstractions;

public interface ITasksDbContext
{
    DbSet<Alert> Alerts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
