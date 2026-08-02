using Hato.Modules.Livestock.Application.Breeds;
using Hato.Modules.People.Domain;
using MediatR;

namespace Hato.Api.Endpoints;

public static class BreedsEndpoints
{
    public static void MapBreedsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/breeds").WithTags("Breeds").RequireAuthorization();

        // Breeds are domain configuration (Art. 8), not day-to-day registration —
        // only Admin can create/change them.
        group.MapPost("/", async (CreateBreedCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/breeds/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
    }
}
