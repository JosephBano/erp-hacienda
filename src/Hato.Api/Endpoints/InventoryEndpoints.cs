using Hato.Modules.Inventory.Application.Consumptions;
using Hato.Modules.Inventory.Application.FeedStages;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Domain;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inventory").WithTags("Inventory").RequireAuthorization();

        group.MapPost("/items", async (CreateInventoryItemCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/items/{id}", new { id });
        });

        group.MapGet("/items", async (ItemCategory? category, ISender sender) =>
        {
            var items = await sender.Send(new GetInventoryItemsQuery(category));
            return Results.Ok(items);
        });

        group.MapGet("/items/{itemId:guid}", async (Guid itemId, ISender sender) =>
            Results.Ok(await sender.Send(new GetInventoryItemByIdQuery(itemId))));

        group.MapGet("/items/{itemId:guid}/batches", async (Guid itemId, ISender sender) =>
            Results.Ok(await sender.Send(new GetInventoryBatchesQuery(itemId))));

        group.MapGet("/items/{itemId:guid}/unit-conversions", async (Guid itemId, ISender sender) =>
            Results.Ok(await sender.Send(new GetInventoryUnitConversionsQuery(itemId))));

        group.MapPost("/items/{itemId:guid}/feed-stage", async (Guid itemId, SetInventoryItemFeedStageCommand command, ISender sender) =>
        {
            await sender.Send(command with { InventoryItemId = itemId });
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryItemsManage));

        group.MapPost("/items/{itemId:guid}/batches", async (Guid itemId, CreateBatchRequest request, ISender sender) =>
        {
            var command = new CreateInventoryBatchCommand(
                itemId, request.BatchNumber, request.Quantity, request.CostPerUnit, request.ExpirationDate);
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/items/{itemId}/batches/{id}", new { id });
        });

        // Per-item unit conversions (PLAN-FASE-3-5-PORCINO.md sec.3.5a.5). The
        // field-app reaches these through the pull once the sync layer is wired;
        // today the admin-web / swagger tooling is the consumer.
        group.MapPost("/items/{itemId:guid}/unit-conversions", async (
            Guid itemId,
            RegisterUnitConversionCommand command,
            ISender sender) =>
        {
            var id = await sender.Send(command with { InventoryItemId = itemId });
            return Results.Created($"/api/v1/inventory/items/{itemId}/unit-conversions/{id}", new { id });
        });

        group.MapPost("/feed-consumptions", async (RecordGroupFeedConsumptionCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/feed-consumptions/{id}", new { id });
        });

        // Feed stage catalog (PLAN-FASE-3-5-PORCINO.md sec.3.5a.5 task 3): preiniciador,
        // iniciador, crecimiento, engorde, gestación, lactancia. Listing only for now —
        // no consumer needs to create/deactivate stages yet (see BACKLOG.md).
        group.MapPost("/feed-stages", async (CreateFeedStageCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/feed-stages/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryFeedStagesManage));

        group.MapPost("/feed-stages/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateFeedStageCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryFeedStagesManage));

        group.MapPost("/feed-stages/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateFeedStageCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryFeedStagesManage));

        group.MapGet("/feed-stages", async (bool? includeInactive, ISender sender) =>
        {
            var stages = await sender.Send(new GetFeedStagesQuery(includeInactive ?? false));
            return Results.Ok(stages);
        });
    }
}

public record CreateBatchRequest(string BatchNumber, decimal Quantity, decimal CostPerUnit, DateOnly? ExpirationDate);
public record RegisterUnitConversionRequest(string FromUnit, string ToUnit, decimal Factor);
