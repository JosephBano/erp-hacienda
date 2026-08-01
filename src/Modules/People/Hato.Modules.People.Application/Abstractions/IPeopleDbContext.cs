using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Abstractions;

public interface IPeopleDbContext
{
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
