using Hato.Modules.Production.Application.Milking;
using MediatR;

namespace Hato.Api.Endpoints;

public static class MilkingEndpoints
{
    public static void MapMilkingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/milking-sessions").WithTags("Milking").RequireAuthorization();

        group.MapPost("/", async (RecordMilkingSessionCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/milking-sessions/{id}", new { id });
        });

        group.MapGet("/", async (DateOnly? date, ISender sender) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var sessions = await sender.Send(new GetDailyMilkingSessionsQuery(targetDate));
            return Results.Ok(sessions);
        });
    }
}
