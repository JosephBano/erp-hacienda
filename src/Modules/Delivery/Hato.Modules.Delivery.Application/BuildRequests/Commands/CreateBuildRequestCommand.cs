using FluentValidation;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Application.DTOs;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Application.BuildRequests.Commands;

public record CreateBuildRequestCommand(
    string Channel,
    string? CommitSha = null,
    string? Version = null,
    string? ReleaseTag = null,
    string? IdempotencyKey = null,
    Guid? UserId = null) : IRequest<CreateBuildRequestResult>;

public class CreateBuildRequestCommandValidator : AbstractValidator<CreateBuildRequestCommand>
{
    private static readonly string[] AllowedChannels = ["stage", "prod", "both"];

    public CreateBuildRequestCommandValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("El canal es obligatorio.")
            .Must(c => AllowedChannels.Contains(c.ToLowerInvariant()))
            .WithMessage("El canal debe ser 'stage', 'prod' o 'both'.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(100).WithMessage("La clave de idempotencia no puede superar los 100 caracteres.");
    }
}

public class CreateBuildRequestHandler(
    IDeliveryDbContext dbContext,
    IVersionCodeReservator versionCodeReservator)
    : IRequestHandler<CreateBuildRequestCommand, CreateBuildRequestResult>
{
    private const string StagePackage = "com.joemandev.hatofieldapp.stage";
    private const string ProdPackage = "com.joemandev.hatofieldapp";
    private const string StageApiUrl = "https://joemanserver.ts.net/api";
    private const string ProdApiUrl = "https://hato-oracle.ts.net/api";

    public async Task<CreateBuildRequestResult> Handle(
        CreateBuildRequestCommand request,
        CancellationToken cancellationToken)
    {
        var channel = request.Channel.ToLowerInvariant().Trim();
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        if (channel == "both")
        {
            return await HandleBothAsync(request, key, cancellationToken);
        }

        return await HandleSingleAsync(request, channel, key, null, cancellationToken);
    }

    private async Task<CreateBuildRequestResult> HandleSingleAsync(
        CreateBuildRequestCommand request,
        string channel,
        string idempotencyKey,
        Guid? groupRequestId,
        CancellationToken cancellationToken)
    {
        // Check for existing request with the same channel and idempotency key
        var existing = await dbContext.MobileBuildRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Channel == channel && r.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            return new CreateBuildRequestResult([MapToDto(existing)], IsDuplicate: true);
        }

        var isStage = channel == MobileReleaseChannels.Stage;
        var packageName = isStage ? StagePackage : ProdPackage;
        var targetApiUrl = isStage ? StageApiUrl : ProdApiUrl;
        var version = !string.IsNullOrWhiteSpace(request.Version)
            ? request.Version.Trim()
            : "1.0.0";
        var commitSha = !string.IsNullOrWhiteSpace(request.CommitSha)
            ? request.CommitSha.Trim()
            : "HEAD";

        // Monotonic transactional reservation of versionCode for the package
        var versionCode = await versionCodeReservator.ReserveNextVersionCodeAsync(packageName, cancellationToken);

        var buildRequest = MobileBuildRequest.Create(
            channel: channel,
            commitSha: commitSha,
            version: version,
            versionCode: versionCode,
            packageName: packageName,
            targetApiUrl: targetApiUrl,
            idempotencyKey: idempotencyKey,
            createdBy: request.UserId,
            groupRequestId: groupRequestId,
            releaseTag: isStage ? null : request.ReleaseTag?.Trim());

        dbContext.MobileBuildRequests.Add(buildRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateBuildRequestResult([MapToDto(buildRequest)], IsDuplicate: false);
    }

    private async Task<CreateBuildRequestResult> HandleBothAsync(
        CreateBuildRequestCommand request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var stageKey = $"{idempotencyKey}:stage";
        var prodKey = $"{idempotencyKey}:prod";

        // Check if both already exist
        var existingStage = await dbContext.MobileBuildRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Channel == MobileReleaseChannels.Stage && r.IdempotencyKey == stageKey, cancellationToken);

        var existingProd = await dbContext.MobileBuildRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Channel == MobileReleaseChannels.Prod && r.IdempotencyKey == prodKey, cancellationToken);

        if (existingStage is not null && existingProd is not null)
        {
            return new CreateBuildRequestResult([MapToDto(existingStage), MapToDto(existingProd)], IsDuplicate: true);
        }

        var groupRequestId = Guid.NewGuid();
        var results = new List<MobileBuildRequestDto>();
        string? prodBlockedReason = null;

        // 1. Create Stage request
        var stageResult = await HandleSingleAsync(
            request with { Channel = MobileReleaseChannels.Stage, ReleaseTag = null },
            MobileReleaseChannels.Stage,
            stageKey,
            groupRequestId,
            cancellationToken);

        results.AddRange(stageResult.Requests);

        // 2. Check if Prod is eligible
        if (string.IsNullOrWhiteSpace(request.ReleaseTag) && string.IsNullOrWhiteSpace(request.CommitSha))
        {
            prodBlockedReason = "Falta una release elegible aprobada de main para compilar producción.";
        }
        else
        {
            var prodResult = await HandleSingleAsync(
                request with { Channel = MobileReleaseChannels.Prod },
                MobileReleaseChannels.Prod,
                prodKey,
                groupRequestId,
                cancellationToken);

            results.AddRange(prodResult.Requests);
        }

        return new CreateBuildRequestResult(results, IsDuplicate: false, ProdBlockedReason: prodBlockedReason);
    }

    private static MobileBuildRequestDto MapToDto(MobileBuildRequest r) =>
        new(
            r.Id,
            r.Channel,
            r.CommitSha,
            r.Version,
            r.VersionCode,
            r.PackageName,
            r.TargetApiUrl,
            r.IdempotencyKey,
            r.Status,
            r.WorkflowRunId,
            r.WorkflowRunAttempt,
            r.ErrorMessage,
            r.ReleaseId,
            r.GroupRequestId,
            r.ReleaseTag,
            r.CreatedAt,
            r.UpdatedAt);
}
