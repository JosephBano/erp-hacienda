using System.Collections.Concurrent;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Infrastructure.CrossModule;

public class UserPermissionsReader(PeopleDbContext dbContext) : IUserPermissionsReader
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _cache = new();

    public async Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var isAdmin = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.User.IsActive && ur.Role != null)
            .AnyAsync(ur => ur.Role.Code == SystemRoles.Admin, cancellationToken);

        HashSet<string> result;
        if (isAdmin)
        {
            var allCodes = await dbContext.Permissions
                .AsNoTracking()
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);

            result = allCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var codes = await dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId && ur.User.IsActive && ur.Role != null)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
                .Distinct()
                .ToListAsync(cancellationToken);

            result = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        _cache.TryAdd(userId, result);
        return result;
    }
}

