using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.People.Infrastructure.Authorization;

public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public class PermissionAuthorizationHandler(IServiceProvider serviceProvider)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        // Admin role override: Admin system role has all permissions (case-insensitive for "admin" / "Admin")
        if (context.User.IsInRole(SystemRoles.Admin) ||
            context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        // 1. Check claim directly in JWT
        if (context.User.HasClaim(c => c.Type == "permission" && c.Value.Equals(requirement.PermissionCode, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        // 2. Query database with per-request scoped cache fallback
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IPeopleDbContext>();

            var hasPermission = await dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId && ur.Role != null && ur.User.IsActive)
                .AnyAsync(ur => ur.Role.Code == SystemRoles.Admin || ur.Role.RolePermissions.Any(rp => rp.Permission.Code == requirement.PermissionCode));

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}

public static class PermissionAuthorizationExtensions
{
    public static AuthorizationPolicyBuilder RequirePermission(this AuthorizationPolicyBuilder builder, string permissionCode)
    {
        return builder.AddRequirements(new PermissionRequirement(permissionCode));
    }
}
