using System.Security.Claims;
using Hato.Modules.Inventory.Application.Consumptions;
using Hato.Modules.Inventory.Application.FeedStages;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Application.Receptions;
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

        // ADR-0026 Decisión 5: legacy POST /batches is kept working (AddBatch path stays
        // for technical/manual adjustments per alternativa D) but is now permission-gated
        // — closing the preexistente gap that every other write endpoint in this module
        // already had. A warning log keeps the call visible in the operational logs so
        // any drift from the new POST /receptions flow is detectable from day one.
        group.MapPost("/items/{itemId:guid}/batches", async (
            Guid itemId,
            CreateBatchRequest request,
            ISender sender,
            ClaimsPrincipal user,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Hato.Api.Endpoints.LegacyBatchCreation");
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            logger.LogWarning(
                "POST /batches called (deprecated per ADR-0026, use POST /receptions). ItemId={ItemId}, UserId={UserId}",
                itemId, userId);
            var command = new CreateInventoryBatchCommand(
                itemId, request.BatchNumber, request.Quantity, request.CostPerUnit, request.ExpirationDate);
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/items/{itemId}/batches/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryItemsManage));

        // ADR-0026 Decisión 5: canonical endpoint for stock entering the finca.
        // Replaces the legacy POST /batches flow for normal operations; carries the
        // operator-declared reception metadata (date, supplier, invoice, author).
        group.MapPost("/items/{itemId:guid}/receptions", async (
            Guid itemId,
            RecordInventoryReceptionRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RecordInventoryReceptionCommand(
                itemId, request.BatchNumber, request.Quantity, request.Unit,
                request.CostPerUnit, request.ExpirationDate, request.ReceivedAt,
                request.SupplierLabel, request.InvoiceReference, request.Notes,
                request.RecordedById, request.RecordedByLabel);
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/items/{itemId}/receptions/{id}", new { id });
        })
        .RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryReceptionsManage))
        .WithName("RecordInventoryReception");

        // Per-item unit conversions (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5). The
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

        // Read-side of the consumption feature (feature/inventory-consumption-history).
        // The panel's "Consumos registrados" section under each item detail calls this
        // to render the history the operator asked for ("cuánto bajó del inventario y demás").
        // Returns 404 when the item id is unknown, 200 + [] when the item is real but
        // has no consumptions yet.
        group.MapGet("/items/{itemId:guid}/consumptions", async (Guid itemId, ISender sender) =>
        {
            var rows = await sender.Send(new GetInventoryConsumptionsQuery(itemId));
            return Results.Ok(rows);
        });

        // Feed stage catalog (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5 task 3): preiniciador,
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
public record RecordInventoryReceptionRequest(
    string BatchNumber,
    decimal Quantity,
    string Unit,
    decimal CostPerUnit,
    DateOnly? ExpirationDate,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? Notes,
    Guid? RecordedById,
    string? RecordedByLabel);
