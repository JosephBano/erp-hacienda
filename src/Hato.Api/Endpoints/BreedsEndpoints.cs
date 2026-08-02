using Hato.Modules.Livestock.Application.Breeds;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class BreedsEndpoints
{
    public static void MapBreedsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/breeds").WithTags("Breeds").RequireAuthorization();

        group.MapPost("/", async (CreateBreedCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/breeds/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockBreedsManage));
    }
}
