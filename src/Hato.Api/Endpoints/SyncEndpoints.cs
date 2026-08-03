using Hato.Api.Sync;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
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

        group.MapGet("/operations", async (string? status, ISender sender) =>
        {
            var result = await sender.Send(new GetSyncOperationsQuery(status));
            return Results.Ok(result);
        });

        // The LWW conflict tray (ADR-0008): admin-only, same as the audit trail it sits
        // alongside — a field an employee didn't cause is not theirs to see resolved.
        group.MapGet("/conflicts", async (string? entityType, ISender sender) =>
        {
            var result = await sender.Send(new GetSyncConflictsQuery(entityType));
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.PeopleUsersManage));
    }
}
