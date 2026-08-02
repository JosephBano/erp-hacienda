using Hato.Modules.Tasks.Application.Alerts;
using MediatR;

namespace Hato.Api.Endpoints;

public static class TasksEndpoints
{
    public static void MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/alerts").WithTags("Alerts").RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var alerts = await sender.Send(new GetActiveAlertsQuery());
            return Results.Ok(alerts);
        });

        group.MapPost("/generate", async (ISender sender) =>
        {
            var count = await sender.Send(new GenerateAlertsCommand());
            return Results.Ok(new { generatedAlerts = count });
        });

        group.MapPost("/{id:guid}/dismiss", async (Guid id, ISender sender) =>
        {
            var success = await sender.Send(new DismissAlertCommand(id));
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
