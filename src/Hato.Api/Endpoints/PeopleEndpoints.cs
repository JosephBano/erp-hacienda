using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Application.Audit;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Roles;
using Hato.Modules.People.Application.Users;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Endpoints;

public static class PeopleEndpoints
{
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public static void MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/people").WithTags("People").RequireAuthorization();

        // Bootstrap or Admin user registration
        group.MapPost("/users", async (RegisterUserCommand command, ISender sender, IPeopleDbContext db, HttpContext http, CancellationToken ct) =>
        {
            await BootstrapLock.WaitAsync(ct);
            try
            {
                var hasAnyUser = await db.Users.AnyAsync(ct);
                if (hasAnyUser && !http.User.IsInRole(SystemRoles.Admin) && !http.User.HasClaim("permission", SystemPermissions.PeopleUsersManage))
                    return Results.Forbid();

                var id = await sender.Send(command, ct);
                return Results.Created($"/api/v1/people/users/{id}", new { id });
            }
            finally
            {
                BootstrapLock.Release();
            }
        }).AllowAnonymous();

        group.MapGet("/users", async (ISender sender) =>
        {
            var users = await sender.Send(new GetUsersQuery());
            return Results.Ok(users);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleUsersManage));

        group.MapPost("/users/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateUserCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleUsersManage));

        group.MapPost("/auth/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        }).AllowAnonymous();

        // Roles & Permissions Endpoints
        group.MapGet("/permissions", async (ISender sender) =>
        {
            var permissions = await sender.Send(new GetPermissionsQuery());
            return Results.Ok(permissions);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleRolesManage));

        group.MapGet("/roles", async (ISender sender) =>
        {
            var roles = await sender.Send(new GetRolesQuery());
            return Results.Ok(roles);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleRolesManage));

        group.MapPost("/roles", async (CreateRoleCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/people/roles/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleRolesManage));

        group.MapPut("/roles/{id:guid}", async (Guid id, UpdateRoleCommand command, ISender sender) =>
        {
            if (id != command.RoleId)
                return Results.BadRequest("El ID de la ruta no coincide con el cuerpo.");

            await sender.Send(command);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleRolesManage));

        group.MapPost("/users/{userId:guid}/roles/{roleId:guid}", async (Guid userId, Guid roleId, ISender sender) =>
        {
            await sender.Send(new AssignUserRoleCommand(userId, roleId));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleRolesManage));

        // Audit Trail Endpoint
        group.MapGet("/audit", async (Guid? userId, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetAuditLogsQuery(userId, from, to, page ?? 1, pageSize ?? 50));
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleUsersManage));

        app.MapGet("/api/v1/audit", async (Guid? userId, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetAuditLogsQuery(userId, from, to, page ?? 1, pageSize ?? 50));
            return Results.Ok(result);
        }).WithTags("Audit").RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleUsersManage));
    }
}
