using Hato.Modules.Livestock.Application.AnimalCategories;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalCategoriesEndpoints
{
    public static void MapAnimalCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animal-categories").WithTags("AnimalCategories").RequireAuthorization();

        group.MapGet("/", async (Guid? speciesId, ISender sender) =>
        {
            var categories = await sender.Send(new GetAnimalCategoriesQuery(speciesId));
            return Results.Ok(categories);
        });

        group.MapPost("/", async (CreateAnimalCategoryCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animal-categories/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockCategoriesManage));
    }
}
