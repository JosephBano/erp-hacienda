using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Hato.SharedKernel;

namespace Hato.Modules.Delivery.Domain;

public class MobileBuildRequest : AuditableEntity
{
    public string Channel { get; private set; } = null!;
    public string CommitSha { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public int VersionCode { get; private set; }
    public string PackageName { get; private set; } = null!;
    public string TargetApiUrl { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public long? WorkflowRunId { get; private set; }
    public int? WorkflowRunAttempt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? ReleaseId { get; private set; }
    public Guid? GroupRequestId { get; private set; }
    public string? ReleaseTag { get; private set; }

    private MobileBuildRequest() { }

    public static MobileBuildRequest Create(
        string channel,
        string commitSha,
        string version,
        int versionCode,
        string packageName,
        string targetApiUrl,
        string idempotencyKey,
        Guid? createdBy = null,
        Guid? groupRequestId = null,
        string? releaseTag = null)
    {
        if (!MobileReleaseChannels.IsValid(channel))
            throw new DeliveryDomainException($"Canal inválido '{channel}'. Debe ser 'stage' o 'prod'.");

        if (string.IsNullOrWhiteSpace(commitSha))
            throw new DeliveryDomainException("El commit SHA es obligatorio.");

        if (string.IsNullOrWhiteSpace(version))
            throw new DeliveryDomainException("La versión es obligatoria.");

        if (versionCode <= 0)
            throw new DeliveryDomainException("El versionCode debe ser un entero positivo.");

        if (string.IsNullOrWhiteSpace(packageName))
            throw new DeliveryDomainException("El packageName es obligatorio.");

        if (string.IsNullOrWhiteSpace(targetApiUrl))
            throw new DeliveryDomainException("La URL de API objetivo es obligatoria.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DeliveryDomainException("La clave de idempotencia es obligatoria.");

        return new MobileBuildRequest
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            CommitSha = commitSha.Trim(),
            Version = version.Trim(),
            VersionCode = versionCode,
            PackageName = packageName.Trim(),
            TargetApiUrl = targetApiUrl.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            Status = MobileBuildStatuses.Requested,
            GroupRequestId = groupRequestId,
            ReleaseTag = releaseTag?.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MarkQueued(long runId, int attempt = 1)
    {
        WorkflowRunId = runId;
        WorkflowRunAttempt = attempt;
        Status = MobileBuildStatuses.Queued;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkBuilding()
    {
        Status = MobileBuildStatuses.Building;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkVerifying()
    {
        Status = MobileBuildStatuses.Verifying;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCandidate(Guid releaseId)
    {
        ReleaseId = releaseId;
        Status = MobileBuildStatuses.Candidate;
        ErrorMessage = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = MobileBuildStatuses.Failed;
        ErrorMessage = error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
