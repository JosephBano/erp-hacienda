using Hato.Modules.Livestock.Application.AnimalCategories;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalCategoriesEndpoints
{
    public static void MapAnimalCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animal-categories").WithTags("AnimalCategories");

        group.MapPost("/", async (CreateAnimalCategoryCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animal-categories/{id}", new { id });
        });
    }
}
