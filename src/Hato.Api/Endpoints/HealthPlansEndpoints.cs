using Hato.Modules.Livestock.Application.HealthPlans;
using Hato.Modules.Livestock.Domain;
using MediatR;

namespace Hato.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the health plan catalog (ADR-0016).
/// The cronogram lives in <c>livestock</c>; the alerts that depend on it
/// (3.5b.2) live in <c>tasks</c> and read by contract.
/// </summary>
public static class HealthPlansEndpoints
{
    public static void MapHealthPlansEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/health-plans")
            .WithTags("HealthPlans").RequireAuthorization();

        group.MapGet("/", async (
            Guid? speciesId,
            bool? includeInactive,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var plans = await sender.Send(
                new GetHealthPlansQuery(speciesId, includeInactive ?? false),
                cancellationToken);
            return Results.Ok(plans);
        });

        group.MapPost("/", async (CreateHealthPlanRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateHealthPlanCommand(request.Name, request.SpeciesId), cancellationToken);
            return Results.Created($"/api/v1/health-plans/{id}", new { id });
        });

        group.MapPost("/{planId:guid}/items", async (
            Guid planId,
            AddHealthPlanItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new AddHealthPlanItemCommand(
                planId,
                request.Name,
                request.EventType,
                request.Anchor,
                request.AnchorOffsetDays,
                request.ComplianceWindowDays,
                request.InventoryItemId,
                request.RouteId,
                request.DoseQuantity,
                request.DoseUnitId,
                request.Repetitions,
                request.AppliesToCategoryId,
                request.AppliesToSex), cancellationToken);
            return Results.Created($"/api/v1/health-plans/{planId}/items/{id}", new { id });
        });

        group.MapPost("/{planId:guid}/assignments", async (
            Guid planId,
            AssignHealthPlanRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new AssignHealthPlanCommand(
                planId, request.AnimalId, request.GroupId), cancellationToken);
            return Results.Created($"/api/v1/health-plans/{planId}/assignments/{id}", new { id });
        });
    }
}

public record CreateHealthPlanRequest(string Name, Guid SpeciesId);

public record AddHealthPlanItemRequest(
    string Name,
    string EventType,
    PlanAnchor Anchor,
    int AnchorOffsetDays,
    int ComplianceWindowDays,
    Guid? InventoryItemId = null,
    Guid? RouteId = null,
    decimal? DoseQuantity = null,
    Guid? DoseUnitId = null,
    int? Repetitions = null,
    Guid? AppliesToCategoryId = null,
    string? AppliesToSex = null);

public record AssignHealthPlanRequest(Guid? AnimalId, Guid? GroupId);
