using Hato.Modules.Livestock.Application.TreatmentCatalogs.AdministrationRoutes;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class AdministrationRoutesEndpoints
{
    public static void MapAdministrationRoutesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/administration-routes")
            .WithTags("AdministrationRoutes")
            .RequireAuthorization();

        group.MapGet("/", async (bool? includeInactive, ISender sender) =>
        {
            var routes = await sender.Send(new GetAdministrationRoutesQuery(includeInactive ?? false));
            return Results.Ok(routes);
        });

        group.MapPost("/", async (CreateAdministrationRouteRequest request, ISender sender) =>
        {
            var id = await sender.Send(new CreateAdministrationRouteCommand(request.Key, request.LabelEs));
            return Results.Created($"/api/v1/administration-routes/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateAdministrationRouteCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateAdministrationRouteCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));

        group.MapPatch("/{id:guid}/label", async (Guid id, UpdateAdministrationRouteLabelRequest request, ISender sender) =>
        {
            await sender.Send(new UpdateAdministrationRouteLabelCommand(id, request.LabelEs));
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockTreatmentsConfigure));
    }
}

public record CreateAdministrationRouteRequest(string Key, string LabelEs);
public record UpdateAdministrationRouteLabelRequest(string LabelEs);