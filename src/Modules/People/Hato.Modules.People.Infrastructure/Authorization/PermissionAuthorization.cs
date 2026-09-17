using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.People.Infrastructure.Authorization;

public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public class PermissionAuthorizationHandler(
    IServiceProvider serviceProvider,
    IHttpContextAccessor? httpContextAccessor = null)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        // Admin role override: Admin system role has all permissions (case-insensitive for "admin" / "Admin").
        // Preserves TestAuthHandler (which runs as synthetic Admin with Guid.Empty) and admin principals.
        if (context.User.IsInRole(SystemRoles.Admin) ||
            context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        // For non-admin users, authorization resolves active permissions from DB/IUserPermissionsReader so revoked permissions are denied.
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
        {
            var reader = httpContextAccessor?.HttpContext?.RequestServices.GetService<IUserPermissionsReader>();
            HashSet<string> permissions;
            if (reader != null)
            {
                permissions = await reader.GetPermissionCodesAsync(userId);
            }
            else
            {
                using var scope = serviceProvider.CreateScope();
                var scopedReader = scope.ServiceProvider.GetRequiredService<IUserPermissionsReader>();
                permissions = await scopedReader.GetPermissionCodesAsync(userId);
            }

            if (permissions.Contains(requirement.PermissionCode))
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
