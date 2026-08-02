using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Users;
using Hato.Modules.People.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Endpoints;

public static class PeopleEndpoints
{
    public static void MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/people").WithTags("People").RequireAuthorization();

        // Bootstrap: anyone may self-register while no Admin exists yet. Once the
        // farm has an Admin, only that Admin can create further accounts.
        group.MapPost("/users", async (RegisterUserCommand command, ISender sender, IPeopleDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var hasAdmin = await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct);
            if (hasAdmin && !http.User.IsInRole(nameof(UserRole.Admin)))
                return Results.Forbid();

            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/people/users/{id}", new { id });
        }).AllowAnonymous();

        group.MapGet("/users", async (ISender sender) =>
        {
            var users = await sender.Send(new GetUsersQuery());
            return Results.Ok(users);
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapPost("/auth/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        }).AllowAnonymous();
    }
}
