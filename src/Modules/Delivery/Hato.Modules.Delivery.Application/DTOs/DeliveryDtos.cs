namespace Hato.Modules.Delivery.Application.DTOs;

public record MobileBuildRequestDto(
    Guid Id,
    string Channel,
    string CommitSha,
    string Version,
    int VersionCode,
    string PackageName,
    string TargetApiUrl,
    string IdempotencyKey,
    string Status,
    long? WorkflowRunId,
    int? WorkflowRunAttempt,
    string? ErrorMessage,
    Guid? ReleaseId,
    Guid? GroupRequestId,
    string? ReleaseTag,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record MobileReleaseTransitionAuditDto(
    Guid Id,
    string FromStatus,
    string ToStatus,
    DateTimeOffset ChangedAt,
    Guid? ChangedBy,
    string? Reason);

public record MobileReleaseDto(
    Guid Id,
    Guid BuildRequestId,
    string Channel,
    string Version,
    int VersionCode,
    string PackageName,
    string TargetApiUrl,
    string CommitSha,
    string? ReleaseTag,
    string ArtifactFileName,
    string ArtifactSha256,
    long ArtifactSizeBytes,
    string SigningCertificateFingerprint,
    string Status,
    bool IsCurrentStable,
    bool IsLastGood,
    string? Notes,
    string ApiCompatibility,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    DateTimeOffset? WithdrawnAt,
    Guid? WithdrawnBy,
    string? WithdrawReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<MobileReleaseTransitionAuditDto>? TransitionAudits = null);

public record CreateBuildRequestResult(
    IReadOnlyCollection<MobileBuildRequestDto> Requests,
    bool IsDuplicate,
    string? ProdBlockedReason = null);
