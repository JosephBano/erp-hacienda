using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Domain;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalGroupsEndpoints
{
    public static void MapAnimalGroupsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animal-groups").WithTags("AnimalGroups").RequireAuthorization();

        group.MapPost("/{id:guid}/events", async (Guid id, RecordGroupEventRequest request, ISender sender) =>
        {
            var command = new RecordGroupEventCommand(
                id, request.EventType, request.OccurredAt, request.RecordedBy,
                request.PayloadJson, request.AffectedCount, request.Cost, request.RelatedEventId);

            var eventId = await sender.Send(command);
            return Results.Created($"/api/v1/animal-groups/{id}/events/{eventId}", new { id = eventId });
        });

        group.MapGet("/{id:guid}/events", async (Guid id, ISender sender) =>
        {
            var events = await sender.Send(new GetGroupEventsQuery(id));
            return Results.Ok(events);
        });

        group.MapGet("/{id:guid}/live-head-count", async (Guid id, ISender sender) =>
        {
            var count = await sender.Send(new GetLiveHeadCountQuery(id));
            return Results.Ok(new { liveHeadCount = count });
        });

        group.MapPost("/", async (CreateAnimalGroupCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animal-groups/{id}", new { id });
        });

        group.MapGet("/", async (bool? includeInactive, ISender sender) =>
        {
            var groups = await sender.Send(new GetAnimalGroupsQuery(includeInactive ?? false));
            return Results.Ok(groups);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var groupItem = await sender.Send(new GetAnimalGroupByIdQuery(id));
            return groupItem is not null ? Results.Ok(groupItem) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, ISender sender) =>
        {
            await sender.Send(new AddGroupMemberCommand(id, request.AnimalId, request.JoinedAt));
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/members/{animalId:guid}", async (Guid id, Guid animalId, DateOnly? leftAt, ISender sender) =>
        {
            await sender.Send(new RemoveGroupMemberCommand(id, animalId, leftAt ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            return Results.NoContent();
        });
    }
}

public record AddMemberRequest(Guid AnimalId, DateOnly JoinedAt);

public record RecordGroupEventRequest(
    EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string PayloadJson,
    int? AffectedCount = null,
    decimal? Cost = null,
    Guid? RelatedEventId = null);
