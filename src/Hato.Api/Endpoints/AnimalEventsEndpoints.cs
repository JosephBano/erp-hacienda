using Hato.Modules.Livestock.Application.Events;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AnimalEventsEndpoints
{
    public static void MapAnimalEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/animals/{animalId:guid}/events").WithTags("AnimalEvents").RequireAuthorization();

        group.MapPost("/", async (Guid animalId, RecordAnimalEventRequest request, ISender sender) =>
        {
            var command = new RecordAnimalEventCommand(
                animalId,
                request.EventType,
                request.OccurredAt,
                request.RecordedBy,
                request.PayloadJson,
                request.Cost,
                request.MilkWithdrawalDays,
                request.MeatWithdrawalDays,
                request.RelatedEventId,
                request.CauseId,
                request.RouteId,
                request.Reason,
                request.BatchId,
                request.HealthPlanItemId,
                request.RecordedById,
                request.AppliedByUserId);

            var eventId = await sender.Send(command);
            return Results.Created($"/api/v1/animals/{animalId}/events/{eventId}", new { id = eventId });
        });

        group.MapGet("/", async (Guid animalId, ISender sender) =>
        {
            var events = await sender.Send(new GetAnimalEventsQuery(animalId));
            return Results.Ok(events);
        });

        app.MapGet("/api/v1/animals/{animalId:guid}/withdrawal-periods", async (Guid animalId, DateOnly? targetDate, ISender sender) =>
        {
            var date = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var withdrawals = await sender.Send(new GetActiveWithdrawalsQuery(animalId, date));
            return Results.Ok(withdrawals);
        }).WithTags("AnimalEvents").RequireAuthorization();
    }
}

public record RecordAnimalEventRequest(
    Hato.Modules.Livestock.Domain.EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string PayloadJson,
    decimal? Cost = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null,
    Guid? RelatedEventId = null,
    Guid? CauseId = null,
    // 3.5a.2-A structured treatment payload. See RecordAnimalEventCommand
    // for the rationale on each field.
    Guid? RouteId = null,
    string? Reason = null,
    Guid? BatchId = null,
    Guid? HealthPlanItemId = null,
    Guid? RecordedById = null,
    Guid? AppliedByUserId = null);
