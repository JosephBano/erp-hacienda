using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Abstractions;

public interface IPeopleDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
