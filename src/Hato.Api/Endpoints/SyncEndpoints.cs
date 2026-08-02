using Hato.Api.Sync;
using MediatR;

namespace Hato.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sync").WithTags("Sync").RequireAuthorization();

        group.MapGet("/pull", async (string? since, string? collections, int? batchSize, ISender sender) =>
        {
            var result = await sender.Send(new GetSyncPullQuery(since, collections, batchSize ?? 500));
            return Results.Ok(result);
        });

        group.MapPost("/push", async (PushSyncBatchCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        });
    }
}
