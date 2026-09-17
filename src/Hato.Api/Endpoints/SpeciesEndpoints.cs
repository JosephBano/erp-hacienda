using Hato.Modules.Livestock.Application.Species;
using Hato.Modules.Livestock.Contracts;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;

namespace Hato.Api.Endpoints;

public static class SpeciesEndpoints
{
    public static void MapSpeciesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/species").WithTags("Species").RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var species = await sender.Send(new GetSpeciesQuery());
            return Results.Ok(species);
        });

        group.MapPost("/", async (CreateSpeciesCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/species/{id}", new { id });
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockSpeciesManage));

        // Lactation parameters (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4): the entity carries
        // DaysOfLactation and CohortWindowDays, but for too long nothing reached them. The
        // PATCH closes the loop end-to-end: the admin-web panel can flip the values, the
        // field app reads the result through the pull, and the cohort-weaning handler
        // stops refusing with "Configure el parámetro en el panel".
        group.MapPatch("/{speciesId:guid}/lactation", async (
            Guid speciesId,
            UpdateSpeciesLactationCommand command,
            ISender sender) =>
        {
            await sender.Send(command with { SpeciesId = speciesId });
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.LivestockSpeciesManage));

        group.MapGet("/{speciesId:guid}/lactation", async (
            Guid speciesId,
            ISender sender) =>
        {
            var profile = await sender.Send(new Hato.Modules.Livestock.Application.Species.GetSpeciesLactationProfileQuery(speciesId));
            return profile is null
                ? Results.NotFound()
                : Results.Ok(profile);
        });
    }
}
