using System.Security.Claims;
using Hato.Modules.Delivery.Application.BuildRequests.Commands;
using Hato.Modules.Delivery.Application.BuildRequests.Queries;
using Hato.Modules.Delivery.Application.Releases.Commands;
using Hato.Modules.Delivery.Application.Releases.Queries;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Hato.Api.Endpoints;

public record CreateBuildApiRequest(
    string Channel,
    string? CommitSha = null,
    string? Version = null,
    string? ReleaseTag = null,
    string? IdempotencyKey = null);

public record PublishReleaseApiRequest(string? Reason = null);

public record WithdrawReleaseApiRequest(string Reason);

public static class MobileReleasesEndpoints
{
    public static void MapMobileReleasesEndpoints(this IEndpointRouteBuilder app)
    {
        // -------------------------------------------------------------
        // Mobile Build Requests
        // -------------------------------------------------------------
        var buildRequestsGroup = app.MapGroup("/api/v1/mobile-build-requests")
            .WithTags("MobileBuildRequests")
            .RequireAuthorization();

        buildRequestsGroup.MapPost("/", async (
            CreateBuildApiRequest body,
            ISender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            Guid? userId = null;
            if (Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) && parsedUserId != Guid.Empty)
            {
                userId = parsedUserId;
            }

            var command = new CreateBuildRequestCommand(
                Channel: body.Channel,
                CommitSha: body.CommitSha,
                Version: body.Version,
                ReleaseTag: body.ReleaseTag,
                IdempotencyKey: body.IdempotencyKey,
                UserId: userId);

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryBuildsManage));

        buildRequestsGroup.MapGet("/", async (
            [FromQuery] string? channel,
            [FromQuery] string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            var requests = await sender.Send(new ListBuildRequestsQuery(channel, status), ct);
            return Results.Ok(requests);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryBuildsManage));

        buildRequestsGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var request = await sender.Send(new GetBuildRequestByIdQuery(id), ct);
            return request is not null ? Results.Ok(request) : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryBuildsManage));

        // -------------------------------------------------------------
        // Mobile Releases
        // -------------------------------------------------------------
        var releasesGroup = app.MapGroup("/api/v1/mobile-releases")
            .WithTags("MobileReleases")
            .RequireAuthorization();

        releasesGroup.MapGet("/", async (
            [FromQuery] string? channel,
            [FromQuery] string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            var releases = await sender.Send(new ListReleasesQuery(channel, status), ct);
            return Results.Ok(releases);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryReleasesDownload));

        releasesGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var release = await sender.Send(new GetReleaseByIdQuery(id), ct);
            return release is not null ? Results.Ok(release) : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryReleasesDownload));

        releasesGroup.MapPost("/{id:guid}/publish", async (
            Guid id,
            PublishReleaseApiRequest? body,
            ISender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) && parsed != Guid.Empty
                ? parsed
                : Guid.Parse("11111111-1111-1111-1111-111111111111");

            var result = await sender.Send(new PublishReleaseCommand(id, userId, body?.Reason), ct);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryReleasesPublish));

        releasesGroup.MapPost("/{id:guid}/withdraw", async (
            Guid id,
            WithdrawReleaseApiRequest body,
            ISender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) && parsed != Guid.Empty
                ? parsed
                : Guid.Parse("11111111-1111-1111-1111-111111111111");

            var result = await sender.Send(new WithdrawReleaseCommand(id, userId, body.Reason), ct);
            return Results.Ok(result);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryReleasesPublish));

        releasesGroup.MapGet("/{id:guid}/download", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            IServiceProvider sp,
            CancellationToken ct) =>
        {
            var hasManage = user.IsInRole(SystemRoles.Admin)
                || user.HasClaim(c => c.Type == ClaimTypes.Role && c.Value.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
                || user.HasClaim("permission", SystemPermissions.DeliveryBuildsManage);

            if (!hasManage && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty)
            {
                var permissionsReader = sp.GetService<IUserPermissionsReader>();
                if (permissionsReader != null)
                {
                    var permissions = await permissionsReader.GetPermissionCodesAsync(userId);
                    hasManage = permissions.Contains(SystemPermissions.DeliveryBuildsManage);
                }
            }

            var result = await sender.Send(new DownloadReleaseQuery(id, hasManage), ct);
            return Results.File(result.ContentStream, result.ContentType, result.FileName, enableRangeProcessing: true);
        }).RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.DeliveryReleasesDownload));
    }
}
