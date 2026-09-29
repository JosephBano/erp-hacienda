using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Xunit;

namespace Hato.Modules.Delivery.UnitTests;

public class MobileReleaseTests
{
    private static MobileRelease CreateSampleRelease()
    {
        return MobileRelease.CreateCandidate(
            buildRequestId: Guid.NewGuid(),
            channel: MobileReleaseChannels.Stage,
            version: "1.0.0",
            versionCode: 1042,
            packageName: "com.joemandev.hatofieldapp.stage",
            targetApiUrl: "https://joemanserver.ts.net/api",
            commitSha: "a1b2c3d4e5f6",
            artifactFileName: "hato-1.0.0-stage-b1042-a1b2c3d.apk",
            artifactSha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            artifactSizeBytes: 45_000_000,
            signingCertificateFingerprint: "14:6D:E2:DA:4A:17:F1:C9:F9:6B:47:89:D2:E6:E0:41:A4:BA:C7:E2:81:7A:B2:7F:6E:9A:F0:4B:9B:C9:83:B0",
            apiCompatibility: ">=1.0.0");
    }

    [Fact]
    public void Create_InitializesInCandidateStatus()
    {
        var release = CreateSampleRelease();

        Assert.Equal(MobileReleaseStatuses.Candidate, release.Status);
        Assert.False(release.IsCurrentStable);
        Assert.False(release.IsLastGood);
        Assert.NotEmpty(release.TransitionAudits);
        Assert.Equal(MobileReleaseStatuses.Candidate, release.TransitionAudits.First().ToStatus);
    }

    [Fact]
    public void Publish_TransitionsToPublishedAndSetsCurrentStable()
    {
        var release = CreateSampleRelease();
        var adminId = Guid.NewGuid();

        release.Publish(adminId, "Aprobado para pruebas internas.");

        Assert.Equal(MobileReleaseStatuses.Published, release.Status);
        Assert.True(release.IsCurrentStable);
        Assert.Equal(adminId, release.PublishedBy);
        Assert.NotNull(release.PublishedAt);

        var publishAudit = release.TransitionAudits.Last();
        Assert.Equal(MobileReleaseStatuses.Candidate, publishAudit.FromStatus);
        Assert.Equal(MobileReleaseStatuses.Published, publishAudit.ToStatus);
        Assert.Equal("Aprobado para pruebas internas.", publishAudit.Reason);
    }

    [Fact]
    public void DemoteCurrentStable_UnsetsFlagAndAudits()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");
        Assert.True(release.IsCurrentStable);

        release.DemoteCurrentStable();

        Assert.False(release.IsCurrentStable);
        Assert.True(release.IsLastGood); // Previous stable becomes last good fallback
    }

    [Fact]
    public void Withdraw_TransitionsToWithdrawnAndRequiresReason()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");

        var adminId = Guid.NewGuid();
        release.Withdraw(adminId, "Regresión detectada en sincronización offline.");

        Assert.Equal(MobileReleaseStatuses.Withdrawn, release.Status);
        Assert.False(release.IsCurrentStable);
        Assert.Equal(adminId, release.WithdrawnBy);
        Assert.NotNull(release.WithdrawnAt);

        var withdrawAudit = release.TransitionAudits.Last();
        Assert.Equal(MobileReleaseStatuses.Published, withdrawAudit.FromStatus);
        Assert.Equal(MobileReleaseStatuses.Withdrawn, withdrawAudit.ToStatus);
        Assert.Equal("Regresión detectada en sincronización offline.", withdrawAudit.Reason);
    }

    [Fact]
    public void Withdraw_WithoutReason_ThrowsException()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");

        Assert.Throws<DeliveryDomainException>(() => release.Withdraw(Guid.NewGuid(), ""));
    }

    [Fact]
    public void InvalidTransition_FromWithdrawnToPublished_Throws()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");
        release.Withdraw(Guid.NewGuid(), "Fallo crítico");

        Assert.Throws<InvalidReleaseTransitionException>(() =>
            release.Publish(Guid.NewGuid(), "Intento de revivir binario retirado"));
    }

    [Fact]
    public void MarkPruned_WhenCurrentStable_ThrowsDeliveryDomainException()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");
        Assert.True(release.IsCurrentStable);

        var ex = Assert.Throws<DeliveryDomainException>(() => release.MarkPruned());
        Assert.Contains("actual estable", ex.Message);
    }

    [Fact]
    public void MarkPruned_WhenLastGood_ThrowsDeliveryDomainException()
    {
        var release = CreateSampleRelease();
        release.Publish(Guid.NewGuid(), "Publicado");
        release.DemoteCurrentStable();
        Assert.True(release.IsLastGood);

        var ex = Assert.Throws<DeliveryDomainException>(() => release.MarkPruned());
        Assert.Contains("última versión buena compatible", ex.Message);
    }

    [Fact]
    public void MarkPruned_WhenUnderInvestigation_ThrowsDeliveryDomainException()
    {
        var release = CreateSampleRelease();
        release.MarkUnderInvestigation(Guid.NewGuid(), "Análisis de bug en campo");
        Assert.Equal(MobileReleaseStatuses.UnderInvestigation, release.Status);

        var ex = Assert.Throws<DeliveryDomainException>(() => release.MarkPruned());
        Assert.Contains("bajo investigación", ex.Message);
    }

    [Fact]
    public void MarkPruned_WhenEligible_TransitionsToPrunedAndRecordsAudit()
    {
        var release = CreateSampleRelease();
        release.MarkPruned(null, "Poda de artefacto antiguo");

        Assert.Equal(MobileReleaseStatuses.Pruned, release.Status);
        Assert.Contains(release.TransitionAudits, a =>
            a.ToStatus == MobileReleaseStatuses.Pruned && a.Reason == "Poda de artefacto antiguo");
    }
}
