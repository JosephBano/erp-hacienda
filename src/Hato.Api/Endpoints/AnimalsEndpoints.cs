using Hato.Modules.Livestock.Application.Animals;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
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
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapGet("/", async (ISender sender) =>
        {
            var animals = await sender.Send(new GetAnimalsQuery());
            return Results.Ok(animals);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsRead));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var animal = await sender.Send(new GetAnimalByIdQuery(id));
            return animal is not null ? Results.Ok(animal) : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsRead));

        group.MapPost("/{id:guid}/identifiers", async (Guid id, AssignAnimalIdentifierRequest request, ISender sender) =>
        {
            var command = new AssignAnimalIdentifierCommand(id, request.Type, request.Value, request.ValidFrom);
            var identifierId = await sender.Send(command);
            return Results.Created($"/api/v1/animals/{id}/identifiers/{identifierId}", new { id = identifierId });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteAnimalCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapGet("/{id:guid}/individual-state", async (Guid id, DateOnly? asOf, ISender sender) =>
        {
            var state = await sender.Send(
                new ResolveIndividualStateQuery(id, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            return Results.Ok(new { state = state.ToString() });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsRead));

        group.MapPut("/{id:guid}", async (Guid id, UpdateAnimalRequest request, ISender sender) =>
        {
            // A direct panel edit is synchronous and online: there is no offline window
            // in which a stale read could have happened, so KnownUpdatedAt is omitted and
            // the write always applies with no conflict to reconcile.
            var command = new UpdateAnimalCommand(
                id, request.BreedId, request.CategoryId, request.BirthDate, DateTimeOffset.UtcNow);
            await sender.Send(command);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));
    }
}

public record AssignAnimalIdentifierRequest(
    Hato.Modules.Livestock.Domain.IdentifierType Type, string Value, DateOnly ValidFrom);

public record UpdateAnimalRequest(Guid? BreedId, Guid? CategoryId, DateOnly? BirthDate);
