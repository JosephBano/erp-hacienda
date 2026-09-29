using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Xunit;

namespace Hato.Modules.Delivery.UnitTests;

public class MobileBuildRequestTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var id = Guid.NewGuid();
        var request = MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "a1b2c3d4e5f6",
            version: "1.0.0",
            versionCode: 1042,
            packageName: "com.joemandev.hatofieldapp.stage",
            targetApiUrl: "https://joemanserver.ts.net/api",
            idempotencyKey: "test-key-1",
            createdBy: id);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(MobileReleaseChannels.Stage, request.Channel);
        Assert.Equal("a1b2c3d4e5f6", request.CommitSha);
        Assert.Equal("1.0.0", request.Version);
        Assert.Equal(1042, request.VersionCode);
        Assert.Equal("com.joemandev.hatofieldapp.stage", request.PackageName);
        Assert.Equal("https://joemanserver.ts.net/api", request.TargetApiUrl);
        Assert.Equal("test-key-1", request.IdempotencyKey);
        Assert.Equal(id, request.CreatedBy);
        Assert.Equal(MobileBuildStatuses.Requested, request.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidChannel_ThrowsException(string? invalidChannel)
    {
        Assert.Throws<DeliveryDomainException>(() =>
            MobileBuildRequest.Create(
                channel: invalidChannel!,
                commitSha: "sha",
                version: "1.0.0",
                versionCode: 1,
                packageName: "com.test",
                targetApiUrl: "https://api.test",
                idempotencyKey: "key"));
    }

    [Fact]
    public void MarkQueued_UpdatesStatusAndRunMetadata()
    {
        var request = MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "sha",
            version: "1.0.0",
            versionCode: 1,
            packageName: "com.test",
            targetApiUrl: "https://api.test",
            idempotencyKey: "key");

        request.MarkQueued(123456L, 1);

        Assert.Equal(MobileBuildStatuses.Queued, request.Status);
        Assert.Equal(123456L, request.WorkflowRunId);
        Assert.Equal(1, request.WorkflowRunAttempt);
    }

    [Fact]
    public void MarkBuilding_TransitionsStatus()
    {
        var request = MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "sha",
            version: "1.0.0",
            versionCode: 1,
            packageName: "com.test",
            targetApiUrl: "https://api.test",
            idempotencyKey: "key");

        request.MarkQueued(123456L, 1);
        request.MarkBuilding();

        Assert.Equal(MobileBuildStatuses.Building, request.Status);
    }

    [Fact]
    public void MarkCandidate_SetsReleaseIdAndCompletes()
    {
        var request = MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "sha",
            version: "1.0.0",
            versionCode: 1,
            packageName: "com.test",
            targetApiUrl: "https://api.test",
            idempotencyKey: "key");

        request.MarkQueued(123456L, 1);
        var releaseId = Guid.NewGuid();
        request.MarkCandidate(releaseId);

        Assert.Equal(MobileBuildStatuses.Candidate, request.Status);
        Assert.Equal(releaseId, request.ReleaseId);
    }

    [Fact]
    public void MarkFailed_SetsErrorMessage()
    {
        var request = MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "sha",
            version: "1.0.0",
            versionCode: 1,
            packageName: "com.test",
            targetApiUrl: "https://api.test",
            idempotencyKey: "key");

        request.MarkQueued(123456L, 1);
        request.MarkFailed("Build timed out in runner.");

        Assert.Equal(MobileBuildStatuses.Failed, request.Status);
        Assert.Equal("Build timed out in runner.", request.ErrorMessage);
    }
}
