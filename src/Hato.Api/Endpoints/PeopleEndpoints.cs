using Hato.Modules.People.Application.Users;
using MediatR;

namespace Hato.Api.Endpoints;

public static class PeopleEndpoints
{
    public static void MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/people").WithTags("People");

        group.MapPost("/users", async (RegisterUserCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/people/users/{id}", new { id });
        });

        group.MapGet("/users", async (ISender sender) =>
        {
            var users = await sender.Send(new GetUsersQuery());
            return Results.Ok(users);
        });
    }
}
