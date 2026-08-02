using Hato.Modules.Livestock.Application.AnimalCategories;
using Hato.Modules.People.Domain;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalCategoriesEndpoints
{
    public static void MapAnimalCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animal-categories").WithTags("AnimalCategories").RequireAuthorization();

        // Animal categories are domain configuration (Art. 8), not day-to-day
        // registration — only Admin can create/change them.
        group.MapPost("/", async (CreateAnimalCategoryCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animal-categories/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
    }
}
