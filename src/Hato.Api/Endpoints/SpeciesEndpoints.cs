using Hato.Modules.Livestock.Application.Species;
using Hato.Modules.People.Domain;
using MediatR;

namespace Hato.Api.Endpoints;

public static class SpeciesEndpoints
{
    public static void MapSpeciesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/species").WithTags("Species").RequireAuthorization();

        // Species are domain configuration (Art. 8), not day-to-day registration —
        // only Admin can create/change them, same as People's user management.
        group.MapPost("/", async (CreateSpeciesCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/species/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
    }
}
