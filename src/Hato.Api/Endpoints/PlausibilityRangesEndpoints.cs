using Hato.Modules.Livestock.Application.PlausibilityRanges;
using MediatR;

namespace Hato.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the <c>plausibility_ranges</c> catalog (ADR-0022).
/// The catalog is data of the field (row-level validation), not panel-level
/// configuration — the GET is exposed to anyone authenticated (same pattern as
/// <c>mortality_causes</c>). Mutating endpoints require the standard
/// <c>LivestockAnimalsWrite</c> permission so the panel can manage them without
/// a new policy added to the permission catalog.
/// </summary>
public static class PlausibilityRangesEndpoints
{
    public static void MapPlausibilityRangesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/plausibility-ranges")
            .WithTags("PlausibilityRanges").RequireAuthorization();

        group.MapGet("/", async (
            Guid? speciesId,
            Guid? categoryId,
            string? magnitude,
            bool? includeInactive,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var ranges = await sender.Send(
                new GetPlausibilityRangesQuery(speciesId, categoryId, magnitude, includeInactive ?? false),
                cancellationToken);
            return Results.Ok(ranges);
        });

        group.MapPost("/", async (CreatePlausibilityRangeRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreatePlausibilityRangeCommand(
                request.SpeciesId,
                request.CategoryId,
                request.Magnitude,
                request.PlausibleMin,
                request.PlausibleMax,
                request.AbsoluteMin,
                request.AbsoluteMax), cancellationToken);
            return Results.Created($"/api/v1/plausibility-ranges/{id}", new { id });
        });

        group.MapPatch("/{id:guid}/bounds", async (
            Guid id,
            UpdatePlausibilityRangeBoundsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdatePlausibilityRangeBoundsCommand(
                id,
                request.PlausibleMin,
                request.PlausibleMax,
                request.AbsoluteMin,
                request.AbsoluteMax), cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeactivatePlausibilityRangeCommand(id), cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ActivatePlausibilityRangeCommand(id), cancellationToken);
            return Results.NoContent();
        });

        // Verdict endpoint: the field-app calls this to classify a value
        // before enqueuing it. The decision is the same one the validator
        // uses server-side. Because the client ships ranges via the pull
        // (ADR-0022 sec.7), this endpoint is a fallback for clients that
        // lack the local table; the field-app won't usually hit it.
        group.MapPost("/evaluate", async (EvaluatePlausibilityRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var verdict = await sender.Send(
                new EvaluatePlausibilityQuery(
                    request.SpeciesId,
                    request.CategoryId,
                    request.Magnitude,
                    request.Value),
                cancellationToken);
            return Results.Ok(new { verdict = verdict.ToString() });
        });
    }
}

public record CreatePlausibilityRangeRequest(
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax);

public record UpdatePlausibilityRangeBoundsRequest(
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax);

public record EvaluatePlausibilityRequest(
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal Value);
