using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Hato.SharedKernel;

namespace Hato.Modules.Delivery.Domain;

public class MobileRelease : AuditableEntity
{
    public Guid BuildRequestId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public int VersionCode { get; private set; }
    public string PackageName { get; private set; } = null!;
    public string TargetApiUrl { get; private set; } = null!;
    public string CommitSha { get; private set; } = null!;
    public string? ReleaseTag { get; private set; }
    public string ArtifactFileName { get; private set; } = null!;
    public string ArtifactSha256 { get; private set; } = null!;
    public long ArtifactSizeBytes { get; private set; }
    public string SigningCertificateFingerprint { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public bool IsCurrentStable { get; private set; }
    public bool IsLastGood { get; private set; }
    public string? Notes { get; private set; }
    public string ApiCompatibility { get; private set; } = null!;
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public Guid? WithdrawnBy { get; private set; }
    public string? WithdrawReason { get; private set; }

    private readonly List<MobileReleaseTransitionAudit> _transitionAudits = [];
    public IReadOnlyCollection<MobileReleaseTransitionAudit> TransitionAudits => _transitionAudits.AsReadOnly();

    private MobileRelease() { }

    public static MobileRelease CreateCandidate(
        Guid buildRequestId,
        string channel,
        string version,
        int versionCode,
        string packageName,
        string targetApiUrl,
        string commitSha,
        string artifactFileName,
        string artifactSha256,
        long artifactSizeBytes,
        string signingCertificateFingerprint,
        string apiCompatibility,
        string? releaseTag = null,
        string? notes = null,
        Guid? createdBy = null)
    {
        if (!MobileReleaseChannels.IsValid(channel))
            throw new DeliveryDomainException($"Canal inválido '{channel}'.");

        if (string.IsNullOrWhiteSpace(artifactSha256) || artifactSha256.Length != 64)
            throw new DeliveryDomainException("El hash SHA-256 del artefacto debe ser una cadena hexadecimal de 64 caracteres.");

        if (artifactSizeBytes <= 0)
            throw new DeliveryDomainException("El tamaño del artefacto debe ser mayor a cero bytes.");

        if (string.IsNullOrWhiteSpace(signingCertificateFingerprint))
            throw new DeliveryDomainException("La huella del certificado de firma es obligatoria.");

        var release = new MobileRelease
        {
            Id = Guid.NewGuid(),
            BuildRequestId = buildRequestId,
            Channel = channel,
            Version = version.Trim(),
            VersionCode = versionCode,
            PackageName = packageName.Trim(),
            TargetApiUrl = targetApiUrl.Trim(),
            CommitSha = commitSha.Trim(),
            ArtifactFileName = artifactFileName.Trim(),
            ArtifactSha256 = artifactSha256.Trim().ToLowerInvariant(),
            ArtifactSizeBytes = artifactSizeBytes,
            SigningCertificateFingerprint = signingCertificateFingerprint.Trim(),
            ApiCompatibility = string.IsNullOrWhiteSpace(apiCompatibility) ? ">= 1.0.0" : apiCompatibility.Trim(),
            ReleaseTag = releaseTag?.Trim(),
            Notes = notes?.Trim(),
            Status = MobileReleaseStatuses.Candidate,
            IsCurrentStable = false,
            IsLastGood = false,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        release._transitionAudits.Add(MobileReleaseTransitionAudit.Create(
            release.Id,
            "None",
            MobileReleaseStatuses.Candidate,
            createdBy,
            "Creación inicial como versión candidata"));

        return release;
    }

    public void Publish(Guid publishedBy, string? reason = null)
    {
        if (!MobileReleaseStatuses.CanPublish(Status))
            throw new InvalidReleaseTransitionException(
                $"No se puede publicar una release en estado '{Status}'. Solo releases en 'Candidate' son publicables.");

        var fromStatus = Status;
        Status = MobileReleaseStatuses.Published;
        IsCurrentStable = true;
        IsLastGood = true;
        PublishedBy = publishedBy;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        _transitionAudits.Add(MobileReleaseTransitionAudit.Create(
            Id,
            fromStatus,
            MobileReleaseStatuses.Published,
            publishedBy,
            reason ?? "Publicación autorizada"));
    }

    public void DemoteCurrentStable()
    {
        IsCurrentStable = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Withdraw(Guid withdrawnBy, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DeliveryDomainException("El motivo de retiro es obligatorio.");

        if (!MobileReleaseStatuses.CanWithdraw(Status))
            throw new InvalidReleaseTransitionException(
                $"No se puede retirar una release en estado '{Status}'. Solo releases 'Published' pueden ser retiradas.");

        var fromStatus = Status;
        Status = MobileReleaseStatuses.Withdrawn;
        IsCurrentStable = false;
        WithdrawnBy = withdrawnBy;
        WithdrawnAt = DateTimeOffset.UtcNow;
        WithdrawReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        _transitionAudits.Add(MobileReleaseTransitionAudit.Create(
            Id,
            fromStatus,
            MobileReleaseStatuses.Withdrawn,
            withdrawnBy,
            reason));
    }
}
