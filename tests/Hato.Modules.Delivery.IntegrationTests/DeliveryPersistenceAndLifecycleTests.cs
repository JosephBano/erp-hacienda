using System.Net;
using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Application.DTOs;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Delivery.IntegrationTests;

public class DeliveryPersistenceAndLifecycleTests(DeliveryApiFactory factory) : IClassFixture<DeliveryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostBuildRequest_CreatesRequest_AndReservesMonotonicVersionCode()
    {
        var idempotencyKey = "test-idempotency-" + Guid.NewGuid().ToString("N");
        var requestBody = new CreateBuildApiRequest(
            Channel: "stage",
            CommitSha: "abcdef123456",
            Version: "1.0.0",
            IdempotencyKey: idempotencyKey);

        var response = await _client.PostAsJsonAsync("/api/v1/mobile-build-requests", requestBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateBuildRequestResult>();
        Assert.NotNull(result);
        Assert.False(result.IsDuplicate);
        Assert.Single(result.Requests);

        var created = result.Requests.First();
        Assert.Equal("stage", created.Channel);
        Assert.Equal("abcdef123456", created.CommitSha);
        Assert.Equal("1.0.0", created.Version);
        Assert.True(created.VersionCode > 0);
        Assert.Equal("com.joemandev.hatofieldapp.stage", created.PackageName);
        Assert.Equal("https://joemanserver.ts.net/api", created.TargetApiUrl);

        // 2. Resubmitting with the same channel and idempotencyKey is idempotent
        var secondResponse = await _client.PostAsJsonAsync("/api/v1/mobile-build-requests", requestBody);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondResult = await secondResponse.Content.ReadFromJsonAsync<CreateBuildRequestResult>();
        Assert.NotNull(secondResult);
        Assert.True(secondResult.IsDuplicate);
        Assert.Single(secondResult.Requests);
        Assert.Equal(created.Id, secondResult.Requests.First().Id);
        Assert.Equal(created.VersionCode, secondResult.Requests.First().VersionCode);
    }

    [Fact]
    public async Task PostBuildRequest_ChannelBoth_GeneratesTwoRequestsWithSharedGroup()
    {
        var idempotencyKey = "both-key-" + Guid.NewGuid().ToString("N");
        var requestBody = new CreateBuildApiRequest(
            Channel: "both",
            CommitSha: "987654fedcba",
            Version: "1.1.0",
            ReleaseTag: "v1.1.0",
            IdempotencyKey: idempotencyKey);

        var response = await _client.PostAsJsonAsync("/api/v1/mobile-build-requests", requestBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateBuildRequestResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Requests.Count);

        var stageReq = result.Requests.FirstOrDefault(r => r.Channel == "stage");
        var prodReq = result.Requests.FirstOrDefault(r => r.Channel == "prod");

        Assert.NotNull(stageReq);
        Assert.NotNull(prodReq);
        Assert.NotNull(stageReq.GroupRequestId);
        Assert.Equal(stageReq.GroupRequestId, prodReq.GroupRequestId);
        Assert.Equal("com.joemandev.hatofieldapp.stage", stageReq.PackageName);
        Assert.Equal("com.joemandev.hatofieldapp", prodReq.PackageName);
        Assert.Equal("https://joemanserver.ts.net/api", stageReq.TargetApiUrl);
        Assert.Equal("https://hato-oracle.ts.net/api", prodReq.TargetApiUrl);
    }

    [Fact]
    public async Task ReleaseLifecycle_Publish_DemotePrevious_Withdraw_And_Download()
    {
        // 1. Seed two candidate releases in database and write binary file to storage
        Guid releaseId1 = Guid.Empty;
        Guid releaseId2 = Guid.Empty;
        var fileName1 = $"hato-1.0.0-prod-b101-{Guid.NewGuid():N}.apk";
        var fileName2 = $"hato-1.0.1-prod-b102-{Guid.NewGuid():N}.apk";
        var dummyApkBytes = "FAKE_SIGNED_APK_BINARY_CONTENT"u8.ToArray();

        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
            using var ms1 = new MemoryStream(dummyApkBytes);
            await storage.SaveArtifactAsync(fileName1, ms1);
            using var ms2 = new MemoryStream(dummyApkBytes);
            await storage.SaveArtifactAsync(fileName2, ms2);
        }

        await factory.ExecuteDbContextAsync(async db =>
        {
            var r1 = MobileRelease.CreateCandidate(
                buildRequestId: Guid.NewGuid(),
                channel: MobileReleaseChannels.Prod,
                version: "1.0.0",
                versionCode: 101,
                packageName: "com.joemandev.hatofieldapp",
                targetApiUrl: "https://hato-oracle.ts.net/api",
                commitSha: "sha-001",
                artifactFileName: fileName1,
                artifactSha256: new string('a', 64),
                artifactSizeBytes: dummyApkBytes.Length,
                signingCertificateFingerprint: "AA:BB:CC:DD",
                apiCompatibility: ">=1.0.0");

            var r2 = MobileRelease.CreateCandidate(
                buildRequestId: Guid.NewGuid(),
                channel: MobileReleaseChannels.Prod,
                version: "1.0.1",
                versionCode: 102,
                packageName: "com.joemandev.hatofieldapp",
                targetApiUrl: "https://hato-oracle.ts.net/api",
                commitSha: "sha-002",
                artifactFileName: fileName2,
                artifactSha256: new string('b', 64),
                artifactSizeBytes: dummyApkBytes.Length,
                signingCertificateFingerprint: "AA:BB:CC:DD",
                apiCompatibility: ">=1.0.0");

            releaseId1 = r1.Id;
            releaseId2 = r2.Id;

            db.MobileReleases.AddRange(r1, r2);
            await db.SaveChangesAsync();
        });

        // 2. Publish release 1
        var pubResp1 = await _client.PostAsJsonAsync($"/api/v1/mobile-releases/{releaseId1}/publish", new PublishReleaseApiRequest("Primera versión producción"));
        Assert.Equal(HttpStatusCode.OK, pubResp1.StatusCode);
        var pubResult1 = await pubResp1.Content.ReadFromJsonAsync<MobileReleaseDto>();
        Assert.NotNull(pubResult1);
        Assert.True(pubResult1.IsCurrentStable);
        Assert.Equal(MobileReleaseStatuses.Published, pubResult1.Status);

        // 3. Publish release 2: should demote release 1 and make release 2 current stable
        var pubResp2 = await _client.PostAsJsonAsync($"/api/v1/mobile-releases/{releaseId2}/publish", new PublishReleaseApiRequest("Segunda versión producción"));
        Assert.Equal(HttpStatusCode.OK, pubResp2.StatusCode);
        var pubResult2 = await pubResp2.Content.ReadFromJsonAsync<MobileReleaseDto>();
        Assert.NotNull(pubResult2);
        Assert.True(pubResult2.IsCurrentStable);

        // Verify in DB that release 1 is no longer current stable
        await factory.ExecuteDbContextAsync(async db =>
        {
            var dbR1 = await db.MobileReleases.FirstAsync(r => r.Id == releaseId1);
            var dbR2 = await db.MobileReleases.FirstAsync(r => r.Id == releaseId2);

            Assert.False(dbR1.IsCurrentStable);
            Assert.True(dbR1.IsLastGood);
            Assert.True(dbR2.IsCurrentStable);
        });

        // 4. Withdraw release 2 with reason
        var withdrawResp = await _client.PostAsJsonAsync($"/api/v1/mobile-releases/{releaseId2}/withdraw", new WithdrawReleaseApiRequest("Problema en campo detectado"));
        Assert.Equal(HttpStatusCode.OK, withdrawResp.StatusCode);
        var withdrawResult = await withdrawResp.Content.ReadFromJsonAsync<MobileReleaseDto>();
        Assert.NotNull(withdrawResult);
        Assert.Equal(MobileReleaseStatuses.Withdrawn, withdrawResult.Status);
        Assert.False(withdrawResult.IsCurrentStable);

        // 5. Download release 1 binary with streaming
        var downloadResp = await _client.GetAsync($"/api/v1/mobile-releases/{releaseId1}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResp.StatusCode);
        Assert.Equal("application/vnd.android.package-archive", downloadResp.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await downloadResp.Content.ReadAsByteArrayAsync();
        Assert.Equal(dummyApkBytes, downloadedBytes);
    }
}
