using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
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
                request.PayloadJson, request.AffectedCount, request.Cost, request.RelatedEventId,
                request.CauseId,
                request.RouteId, request.Reason, request.BatchId, request.HealthPlanItemId,
                request.RecordedById, request.AppliedByUserId);

            var eventId = await sender.Send(command);
            return Results.Created($"/api/v1/animal-groups/{id}/events/{eventId}", new { id = eventId });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

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

        // === ADR-0025 PR2: CRUD endpoints + policy hardening ===

        // ADR-0025 sec.2: writing groups shares the same permission as writing animals.
        // The pre-PR2 endpoint had RequireAuthorization() only — a pre-existing gap closed
        // here so the same operator who can edit animals can manage groups.
        group.MapPost("/", async (CreateAnimalGroupCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/animal-groups/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapPut("/{id:guid}", async (Guid id, UpdateAnimalGroupRequest request, ISender sender) =>
        {
            await sender.Send(new UpdateAnimalGroupCommand(id, request.Name, request.Description, request.SpeciesId));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        // ADR-0025 sec.2: "delete" is a soft deactivation (IsActive = false), reversible
        // via POST /activate. Maps 1:1 with the domain Deactivate() / Activate() pair.
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateAnimalGroupCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateAnimalGroupCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        // ADR-0025 sec.3 + ADR-0015 sec.7 trap: changing tracking mode once the group has
        // members or events would fabricate identity within an anonymous lot. The handler
        // rejects with AnimalGroupStateException → 409 (Conflict). The endpoint does not
        // need its own 409 mapping because the exception handler does it.
        group.MapPatch("/{id:guid}/tracking-mode", async (Guid id, ChangeAnimalGroupTrackingModeRequest request, ISender sender) =>
        {
            await sender.Send(new ChangeAnimalGroupTrackingModeCommand(id, request.TrackingMode));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

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

        // Lot summary (PLAN-FASE-3-5-PORCINO.md sec.3.5a.7 task 6): the screen the
        // field-app shows when the operator taps on a headcount lot. Live head count
        // plus the most recent event of each kind, recomputed on every read.
        group.MapGet("/{id:guid}/summary", async (Guid id, ISender sender) =>
        {
            var summary = await sender.Send(new GetAnimalGroupSummaryQuery(id));
            return Results.Ok(summary);
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, ISender sender) =>
        {
            await sender.Send(new AddGroupMemberCommand(id, request.AnimalId, request.JoinedAt));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));

        group.MapDelete("/{id:guid}/members/{animalId:guid}", async (Guid id, Guid animalId, DateOnly? leftAt, ISender sender) =>
        {
            await sender.Send(new RemoveGroupMemberCommand(id, animalId, leftAt ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockAnimalsWrite));
    }
}

public record AddMemberRequest(Guid AnimalId, DateOnly JoinedAt);

public record UpdateAnimalGroupRequest(string Name, string? Description, Guid? SpeciesId);

public record ChangeAnimalGroupTrackingModeRequest(TrackingMode TrackingMode);

public record RecordGroupEventRequest(
    EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string PayloadJson,
    int? AffectedCount = null,
    decimal? Cost = null,
    Guid? RelatedEventId = null,
    Guid? CauseId = null,
    // 3.5a.2-A structured treatment payload. See RecordGroupEventCommand
    // for the rationale on each field.
    Guid? RouteId = null,
    string? Reason = null,
    Guid? BatchId = null,
    Guid? HealthPlanItemId = null,
    Guid? RecordedById = null,
    Guid? AppliedByUserId = null);
