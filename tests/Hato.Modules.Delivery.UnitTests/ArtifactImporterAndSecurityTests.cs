using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Hato.Modules.Delivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hato.Modules.Delivery.UnitTests;

public class ArtifactImporterAndSecurityTests
{
    private readonly FakeArtifactStorage _storage = new();
    private readonly FakeRetentionPolicyService _retention = new();
    private readonly FakeExternalArtifactUploader _uploader = new();
    private readonly ArtifactImporter _importer;

    public ArtifactImporterAndSecurityTests()
    {
        _importer = new ArtifactImporter(
            new DummyDeliveryDbContext(),
            _storage,
            _retention,
            _uploader,
            NullLogger<ArtifactImporter>.Instance);
    }

    [Theory]
    [InlineData("../../etc/passwd.apk")]
    [InlineData("../malicious.apk")]
    [InlineData("/var/tmp/hato-1.0.0-stage-b100-abc1234.apk")]
    [InlineData("hato-1.0.0-stage-b100-abc1234.apk\0extra")]
    [InlineData("arbitrary_name.apk")]
    [InlineData("hato-1.0.0-unknown-b100-abc1234.apk")]
    public async Task ImportArtifact_WithInvalidOrTraversalFileName_ThrowsDeliverySecurityException(string maliciousFileName)
    {
        var request = CreateSampleRequest();
        var zipBytes = CreateZipWithManifest(maliciousFileName, "sha", 10, request.CommitSha, request.VersionCode, request.PackageName, [1, 2, 3]);

        using var zipStream = new MemoryStream(zipBytes);

        await Assert.ThrowsAsync<DeliverySecurityException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));
    }

    [Fact]
    public async Task ImportArtifact_WhenSha256Mismatch_ThrowsArtifactIntegrityException()
    {
        var request = CreateSampleRequest();
        var apkBytes = new byte[] { 1, 2, 3, 4, 5 };
        var wrongSha256 = new string('a', 64);
        var fileName = "hato-1.0.0-stage-b100-abc1234.apk";

        var zipBytes = CreateZipWithManifest(fileName, wrongSha256, apkBytes.Length, request.CommitSha, request.VersionCode, request.PackageName, apkBytes);
        using var zipStream = new MemoryStream(zipBytes);

        var ex = await Assert.ThrowsAsync<ArtifactIntegrityException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));

        Assert.Contains("hash SHA-256", ex.Message);
    }

    [Fact]
    public async Task ImportArtifact_WhenSizeBytesMismatch_ThrowsArtifactIntegrityException()
    {
        var request = CreateSampleRequest();
        var apkBytes = new byte[] { 1, 2, 3, 4, 5 };
        var realSha256 = Convert.ToHexString(SHA256.HashData(apkBytes)).ToLowerInvariant();
        var fileName = "hato-1.0.0-stage-b100-abc1234.apk";

        var zipBytes = CreateZipWithManifest(fileName, realSha256, declaredSizeBytes: 9999, request.CommitSha, request.VersionCode, request.PackageName, apkBytes);
        using var zipStream = new MemoryStream(zipBytes);

        var ex = await Assert.ThrowsAsync<ArtifactIntegrityException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));

        Assert.Contains("tamaño real del APK", ex.Message);
    }

    [Fact]
    public async Task ImportArtifact_WhenCommitShaMismatch_ThrowsArtifactIntegrityException()
    {
        var request = CreateSampleRequest();
        var apkBytes = new byte[] { 1, 2, 3 };
        var realSha256 = Convert.ToHexString(SHA256.HashData(apkBytes)).ToLowerInvariant();
        var fileName = "hato-1.0.0-stage-b100-abc1234.apk";

        var zipBytes = CreateZipWithManifest(fileName, realSha256, apkBytes.Length, "foreign-commit-sha", request.VersionCode, request.PackageName, apkBytes);
        using var zipStream = new MemoryStream(zipBytes);

        var ex = await Assert.ThrowsAsync<ArtifactIntegrityException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));

        Assert.Contains("commit SHA del artefacto", ex.Message);
    }

    [Fact]
    public async Task ImportArtifact_WhenVersionCodeMismatch_ThrowsArtifactIntegrityException()
    {
        var request = CreateSampleRequest();
        var apkBytes = new byte[] { 1, 2, 3 };
        var realSha256 = Convert.ToHexString(SHA256.HashData(apkBytes)).ToLowerInvariant();
        var fileName = "hato-1.0.0-stage-b100-abc1234.apk";

        var zipBytes = CreateZipWithManifest(fileName, realSha256, apkBytes.Length, request.CommitSha, 9999, request.PackageName, apkBytes);
        using var zipStream = new MemoryStream(zipBytes);

        var ex = await Assert.ThrowsAsync<ArtifactIntegrityException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));

        Assert.Contains("versionCode del artefacto", ex.Message);
    }

    [Fact]
    public async Task ImportArtifact_WhenStorageQuotaExceeded_ThrowsStorageQuotaExceededException()
    {
        var request = CreateSampleRequest();
        var apkBytes = new byte[] { 1, 2, 3 };
        var realSha256 = Convert.ToHexString(SHA256.HashData(apkBytes)).ToLowerInvariant();
        var fileName = "hato-1.0.0-stage-b100-abc1234.apk";

        // Almacenamiento ya excede los 5 GiB
        _storage.TotalStorageBytes = 5L * 1024 * 1024 * 1024 + 100;

        var zipBytes = CreateZipWithManifest(fileName, realSha256, apkBytes.Length, request.CommitSha, request.VersionCode, request.PackageName, apkBytes);
        using var zipStream = new MemoryStream(zipBytes);

        var ex = await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
            _importer.ImportArtifactAsync(request, zipStream));

        Assert.Contains("límite presupuestado de 5 GiB", ex.Message);
    }

    private static MobileBuildRequest CreateSampleRequest()
    {
        return MobileBuildRequest.Create(
            channel: MobileReleaseChannels.Stage,
            commitSha: "abc1234567890abcdef1234567890abcdef12345",
            version: "1.0.0",
            versionCode: 100,
            packageName: "com.joemandev.hatofieldapp.stage",
            targetApiUrl: "https://joemanserver.ts.net/api",
            idempotencyKey: "idem-001");
    }

    private static byte[] CreateZipWithManifest(
        string apkFileName,
        string sha256,
        long declaredSizeBytes,
        string commitSha,
        int versionCode,
        string packageName,
        byte[] apkContent)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var manifestObj = new
            {
                artifact_file_name = apkFileName,
                sha256 = sha256,
                artifact_size_bytes = declaredSizeBytes,
                commit_sha = commitSha,
                version_code = versionCode,
                version = "1.0.0",
                package_name = packageName,
                target_api_url = "https://joemanserver.ts.net/api",
                signing_certificate_fingerprint = "AA:BB:CC:DD"
            };

            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(JsonSerializer.Serialize(manifestObj));
            }

            if (!string.IsNullOrEmpty(apkFileName) && !apkFileName.Contains('/') && !apkFileName.Contains('\\'))
            {
                var apkEntry = archive.CreateEntry(apkFileName);
                using var apkStream = apkEntry.Open();
                apkStream.Write(apkContent, 0, apkContent.Length);
            }
        }

        return memoryStream.ToArray();
    }

    private sealed class FakeArtifactStorage : IArtifactStorage
    {
        public long TotalStorageBytes { get; set; } = 100L * 1024 * 1024;
        public string GetStoragePath() => "/srv/hato-production/mobile-artifacts";
        public Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Stream?> OpenReadStreamAsync(string fileName, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
        public Task SaveArtifactAsync(string fileName, Stream contentStream, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeleteIfExistsAsync(string fileName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<long> GetTotalStorageBytesAsync(CancellationToken cancellationToken = default) => Task.FromResult(TotalStorageBytes);
    }

    private sealed class FakeRetentionPolicyService : IRetentionPolicyService
    {
        public Task<RetentionExecutionResult> ApplyRetentionAsync(string channel, bool dryRun = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RetentionExecutionResult(channel, 0, 0, 0, 0, []));
    }

    private sealed class FakeExternalArtifactUploader : IExternalArtifactUploader
    {
        public Task<bool> UploadArtifactToDriveAsync(string localApkPath, string channel, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class DummyDeliveryDbContext : IDeliveryDbContext
    {
        public DbSet<MobileBuildRequest> MobileBuildRequests => throw new NotImplementedException();
        public DbSet<MobileRelease> MobileReleases => throw new NotImplementedException();
        public DbSet<MobileReleaseTransitionAudit> MobileReleaseTransitionAudits => throw new NotImplementedException();
        public DbSet<MobileVersionCodeSequence> MobileVersionCodeSequences => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public DatabaseFacade Database => throw new NotImplementedException();
    }
}
