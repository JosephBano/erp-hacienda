using Hato.Modules.Livestock.Application.MortalityCauses;
using MediatR;

namespace Hato.Api.Endpoints;

public static class MortalityCausesEndpoints
{
    public static void MapMortalityCausesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/mortality-causes").WithTags("MortalityCauses").RequireAuthorization();

        group.MapPost("/", async (CreateMortalityCauseRequest request, ISender sender) =>
        {
            var id = await sender.Send(new CreateMortalityCauseCommand(request.Name));
            return Results.Created($"/api/v1/mortality-causes/{id}", new { id });
        });

        group.MapGet("/", async (bool? includeInactive, ISender sender) =>
        {
            var causes = await sender.Send(new GetMortalityCausesQuery(includeInactive ?? false));
            return Results.Ok(causes);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateMortalityCauseCommand(id));
            return Results.NoContent();
        });
    }
}

public record CreateMortalityCauseRequest(string Name);
