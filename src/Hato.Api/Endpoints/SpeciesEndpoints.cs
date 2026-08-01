using Hato.Modules.Livestock.Application.Species;
using MediatR;

namespace Hato.Api.Endpoints;

public static class SpeciesEndpoints
{
    public static void MapSpeciesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/species").WithTags("Species");

        group.MapPost("/", async (CreateSpeciesCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/species/{id}", new { id });
        });
    }
}
