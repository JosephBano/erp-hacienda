using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hato.Modules.People.Application.Abstractions;

public interface IPeopleDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SyncOperation> SyncOperations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Needed to detach an entity whose INSERT lost a race on a unique index. Without
    /// detaching, the failed entity stays in the change tracker and poisons every later
    /// <c>SaveChanges</c> in the same request — which, in a sync push, means one duplicate
    /// operation would take the rest of the batch down with it.
    /// </summary>
    EntityEntry Entry(object entity);
}
