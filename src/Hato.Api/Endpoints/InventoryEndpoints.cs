using Hato.Modules.Inventory.Application.Consumptions;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Domain;
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

        group.MapPost("/items/{itemId:guid}/batches", async (Guid itemId, CreateBatchRequest request, ISender sender) =>
        {
            var command = new CreateInventoryBatchCommand(
                itemId, request.BatchNumber, request.Quantity, request.CostPerUnit, request.ExpirationDate);
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/items/{itemId}/batches/{id}", new { id });
        });

        group.MapPost("/feed-consumptions", async (RecordGroupFeedConsumptionCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/inventory/feed-consumptions/{id}", new { id });
        });
    }
}

public record CreateBatchRequest(string BatchNumber, decimal Quantity, decimal CostPerUnit, DateOnly? ExpirationDate);
