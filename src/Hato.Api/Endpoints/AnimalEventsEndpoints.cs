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
                request.CauseId);

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
    Guid? CauseId = null);
