using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Users;
using Hato.Modules.People.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Endpoints;

public static class PeopleEndpoints
{
    // Guards the bootstrap check-then-create below. Without it, two concurrent
    // anonymous requests can both observe "no users yet" and both slip through as
    // self-elevated Admins before either INSERT commits — a classic TOCTOU race.
    // Process-local is sufficient: this API runs as a single instance (Art. 1
    // ARCHITECTURE.md — "un deploy"), not horizontally scaled.
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public static void MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/people").WithTags("People").RequireAuthorization();

        // Bootstrap: self-registration is only allowed while the farm has zero users
        // at all — the single very first account. Checking "no Admin yet" instead
        // would leave the endpoint open indefinitely to anyone willing to never pick
        // the Admin role, then self-elevate whenever they choose.
        group.MapPost("/users", async (RegisterUserCommand command, ISender sender, IPeopleDbContext db, HttpContext http, CancellationToken ct) =>
        {
            await BootstrapLock.WaitAsync(ct);
            try
            {
                var hasAnyUser = await db.Users.AnyAsync(ct);
                if (hasAnyUser && !http.User.IsInRole(nameof(UserRole.Admin)))
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
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapPost("/users/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateUserCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapPost("/auth/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        }).AllowAnonymous();
    }
}
