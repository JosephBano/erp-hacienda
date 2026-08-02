using Hato.Modules.Livestock.Application.Animals;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalsEndpoints
{
    public static void MapAnimalsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animals").WithTags("Animals").RequireAuthorization();

        group.MapPost("/", async (RegisterAnimalCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animals/{id}", new { id });
        });

        group.MapGet("/", async (ISender sender) =>
        {
            var animals = await sender.Send(new GetAnimalsQuery());
            return Results.Ok(animals);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var animal = await sender.Send(new GetAnimalByIdQuery(id));
            return animal is not null ? Results.Ok(animal) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/identifiers", async (Guid id, AssignAnimalIdentifierRequest request, ISender sender) =>
        {
            var command = new AssignAnimalIdentifierCommand(id, request.Type, request.Value, request.ValidFrom);
            var identifierId = await sender.Send(command);
            return Results.Created($"/api/v1/animals/{id}/identifiers/{identifierId}", new { id = identifierId });
        });
    }
}

public record AssignAnimalIdentifierRequest(
    Hato.Modules.Livestock.Domain.IdentifierType Type, string Value, DateOnly ValidFrom);
