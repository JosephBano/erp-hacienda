using Hato.Modules.Livestock.Application.TreatmentCatalogs.TreatmentReasons;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class TreatmentReasonsEndpoints
{
    public static void MapTreatmentReasonsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/treatment-reasons")
            .WithTags("TreatmentReasons")
            .RequireAuthorization();

        group.MapGet("/", async (bool? includeInactive, ISender sender) =>
        {
            var reasons = await sender.Send(new GetTreatmentReasonsQuery(includeInactive ?? false));
            return Results.Ok(reasons);
        });

        group.MapPost("/", async (CreateTreatmentReasonRequest request, ISender sender) =>
        {
            var id = await sender.Send(new CreateTreatmentReasonCommand(request.Key, request.LabelEs));
            return Results.Created($"/api/v1/treatment-reasons/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateTreatmentReasonCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateTreatmentReasonCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapPatch("/{id:guid}/label", async (Guid id, UpdateTreatmentReasonLabelRequest request, ISender sender) =>
        {
            await sender.Send(new UpdateTreatmentReasonLabelCommand(id, request.LabelEs));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));
    }
}

public record CreateTreatmentReasonRequest(string Key, string LabelEs);
public record UpdateTreatmentReasonLabelRequest(string LabelEs);