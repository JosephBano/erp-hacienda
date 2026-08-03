using Hato.Modules.People.Contracts;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Infrastructure.CrossModule;

public class UserPermissionsReader(PeopleDbContext dbContext) : IUserPermissionsReader
{
    public async Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var codes = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.User.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct()
            .ToListAsync(cancellationToken);

        return codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
