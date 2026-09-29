using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Delivery.IntegrationTests;

[Collection("Database")]
public class DeliveryWorkerOrchestrationTests(DeliveryApiFactory factory) : IClassFixture<DeliveryApiFactory>
{
    [Fact]
    public async Task WorkerRecoveryAfterCrash_DoesNotDuplicateDispatch_AndImportsCompletedArtifact()
    {
        var commitSha = "a1b2c3d4e5f67890abcdef1234567890" + Guid.NewGuid().ToString("N")[..8];
        var versionCode = 201;
        var runId = 987654L;
        var fileName = $"hato-1.0.0-stage-b{versionCode}-{commitSha[..7]}.apk";
        var apkBytes = new byte[] { 10, 20, 30, 40, 50, 60 };
        var realSha256 = Convert.ToHexString(SHA256.HashData(apkBytes)).ToLowerInvariant();

        // 1. Simular solicitud que quedó en Building antes del reinicio
        Guid requestId = Guid.Empty;
        await factory.ExecuteDbContextAsync(async db =>
        {
            var req = MobileBuildRequest.Create(
                channel: MobileReleaseChannels.Stage,
                commitSha: commitSha,
                version: "1.0.0",
                versionCode: versionCode,
                packageName: "com.joemandev.hatofieldapp.stage",
                targetApiUrl: "https://joemanserver.ts.net/api",
                idempotencyKey: "idem-" + Guid.NewGuid());

            req.MarkBuilding();
            db.MobileBuildRequests.Add(req);
            await db.SaveChangesAsync();
            requestId = req.Id;
        });

        // 2. Configurar GitHub Actions simulado con run exitoso y artefacto
        factory.GitHubClient.DispatchedRequests.Clear();
        factory.GitHubClient.Runs =
        [
            new GitHubWorkflowRunDto(
                Id: runId,
                RunAttempt: 1,
                WorkflowName: "build-android.yml",
                HeadSha: commitSha,
                Status: "completed",
                Conclusion: "success",
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow)
        ];

        factory.GitHubClient.ArtifactStreamFactory = (id, name) =>
        {
            if (id == runId)
            {
                var zip = CreateZipPackage(fileName, realSha256, apkBytes.Length, commitSha, versionCode, "com.joemandev.hatofieldapp.stage", apkBytes);
                return new MemoryStream(zip);
            }
            return null;
        };

        // 3. Ejecutar ciclo del worker
        var worker = factory.Services.GetRequiredService<MobileDeliveryWorker>();
        var processed = await worker.ProcessCycleAsync();

        Assert.True(processed > 0);

        // 4. Verificar que NO hubo nuevo dispatch (se recuperó la solicitud existente)
        Assert.Empty(factory.GitHubClient.DispatchedRequests);

        // 5. Verificar estado en base de datos PostgreSQL real
        await factory.ExecuteDbContextAsync(async db =>
        {
            var req = await db.MobileBuildRequests.FirstAsync(r => r.Id == requestId);
            Assert.True(req.Status == MobileBuildStatuses.Candidate, $"Request failed with ErrorMessage: '{req.ErrorMessage}'");
            Assert.Equal(MobileBuildStatuses.Candidate, req.Status);
            Assert.Equal(runId, req.WorkflowRunId);
            Assert.NotNull(req.ReleaseId);

            var release = await db.MobileReleases.FirstAsync(r => r.Id == req.ReleaseId);
            Assert.Equal(versionCode, release.VersionCode);
            Assert.Equal(fileName, release.ArtifactFileName);
            Assert.Equal(realSha256, release.ArtifactSha256);
            Assert.Equal(MobileReleaseStatuses.Candidate, release.Status);
        });

        // 6. Verificar que el archivo APK existe en el directorio de artefactos
        var expectedPath = Path.Combine(factory.ArtifactDirectory, fileName);
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task Worker_RejectsForeignRun_WhenCommitShaDoesNotMatch()
    {
        var myCommitSha = "sha-mine-" + Guid.NewGuid().ToString("N")[..8];
        var foreignSha = "sha-foreign-" + Guid.NewGuid().ToString("N")[..8];
        var versionCode = 305;

        Guid requestId = Guid.Empty;
        await factory.ExecuteDbContextAsync(async db =>
        {
            var req = MobileBuildRequest.Create(
                channel: MobileReleaseChannels.Stage,
                commitSha: myCommitSha,
                version: "1.0.0",
                versionCode: versionCode,
                packageName: "com.joemandev.hatofieldapp.stage",
                targetApiUrl: "https://joemanserver.ts.net/api",
                idempotencyKey: "idem-" + Guid.NewGuid());

            req.MarkBuilding();
            db.MobileBuildRequests.Add(req);
            await db.SaveChangesAsync();
            requestId = req.Id;
        });

        factory.GitHubClient.Runs =
        [
            new GitHubWorkflowRunDto(
                Id: 555555,
                RunAttempt: 1,
                WorkflowName: "build-android.yml",
                HeadSha: foreignSha, // SHA ajeno
                Status: "completed",
                Conclusion: "success",
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow)
        ];

        var worker = factory.Services.GetRequiredService<MobileDeliveryWorker>();
        await worker.ProcessCycleAsync();

        // La solicitud debe continuar en Building, rechazando el run ajeno
        await factory.ExecuteDbContextAsync(async db =>
        {
            var req = await db.MobileBuildRequests.FirstAsync(r => r.Id == requestId);
            Assert.Equal(MobileBuildStatuses.Building, req.Status);
            Assert.Null(req.WorkflowRunId);
        });
    }

    [Fact]
    public async Task Worker_EnforcesConcurrencyQuota_OneBuildPerChannel()
    {
        Guid reqBuildingId = Guid.Empty;
        Guid reqQueuedId = Guid.Empty;

        await factory.ExecuteDbContextAsync(async db =>
        {
            var req1 = MobileBuildRequest.Create(
                channel: MobileReleaseChannels.Stage,
                commitSha: "sha-001",
                version: "1.0.0",
                versionCode: 401,
                packageName: "com.joemandev.hatofieldapp.stage",
                targetApiUrl: "https://joemanserver.ts.net/api",
                idempotencyKey: "idem-" + Guid.NewGuid());
            req1.MarkBuilding();

            var req2 = MobileBuildRequest.Create(
                channel: MobileReleaseChannels.Stage,
                commitSha: "sha-002",
                version: "1.0.0",
                versionCode: 402,
                packageName: "com.joemandev.hatofieldapp.stage",
                targetApiUrl: "https://joemanserver.ts.net/api",
                idempotencyKey: "idem-" + Guid.NewGuid());
            req2.MarkQueued(0);

            db.MobileBuildRequests.AddRange(req1, req2);
            await db.SaveChangesAsync();

            reqBuildingId = req1.Id;
            reqQueuedId = req2.Id;
        });

        factory.GitHubClient.DispatchedRequests.Clear();
        factory.GitHubClient.Runs = [];

        var worker = factory.Services.GetRequiredService<MobileDeliveryWorker>();
        await worker.ProcessCycleAsync();

        // req2 NO debió ser despachada porque ya hay una en curso para 'stage'
        await factory.ExecuteDbContextAsync(async db =>
        {
            var req2 = await db.MobileBuildRequests.FirstAsync(r => r.Id == reqQueuedId);
            Assert.Equal(MobileBuildStatuses.Queued, req2.Status);
        });

        Assert.DoesNotContain(factory.GitHubClient.DispatchedRequests, r => r.Id == reqQueuedId);
    }

    [Fact]
    public async Task RetentionPolicy_AgainstPostgreSql_ProtectsStableAndPrunesExcess()
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
        var retentionService = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();

        var testChannel = MobileReleaseChannels.Stage;

        var releases = new List<MobileRelease>();
        var dummyApkBytes = new byte[] { 1, 2, 3 };

        // Crear 12 releases para este canal:
        // 0: Actual estable (protegida)
        // 1: Última buena (protegida)
        // 2: Bajo investigación (protegida)
        // 3..11: Candidatas regulares (las más viejas deben ser podadas si superan 10)
        await factory.ExecuteDbContextAsync(async db =>
        {
            for (int i = 0; i < 12; i++)
            {
                var fileName = $"hato-1.0.0-stage-b{500 + i}-abcdef{i:D2}.apk";
                var r = MobileRelease.CreateCandidate(
                    buildRequestId: Guid.NewGuid(),
                    channel: testChannel,
                    version: "1.0.0",
                    versionCode: 500 + i,
                    packageName: "com.joemandev.hatofieldapp.stage",
                    targetApiUrl: "https://joemanserver.ts.net/api",
                    commitSha: $"sha-{i:D3}",
                    artifactFileName: fileName,
                    artifactSha256: new string((char)('a' + (i % 26)), 64),
                    artifactSizeBytes: dummyApkBytes.Length,
                    signingCertificateFingerprint: "AA:BB",
                    apiCompatibility: ">=1.0.0");

                if (i == 0)
                {
                    r.Publish(Guid.NewGuid(), "Estable");
                }
                else if (i == 1)
                {
                    r.Publish(Guid.NewGuid(), "Estable anterior");
                    r.DemoteCurrentStable(); // IsLastGood = true
                }
                else if (i == 2)
                {
                    r.MarkUnderInvestigation(Guid.NewGuid(), "Bajo investigación");
                }

                releases.Add(r);
                db.MobileReleases.Add(r);

                // Crear archivo físico en storage
                using var stream = new MemoryStream(dummyApkBytes);
                await storage.SaveArtifactAsync(fileName, stream);
            }

            await db.SaveChangesAsync();
        });

        // Ejecutar retención
        var result = await retentionService.ApplyRetentionAsync(testChannel, dryRun: false);

        Assert.True(result.ProtectedReleases >= 3, $"Protected was {result.ProtectedReleases}");
        Assert.True(result.PrunedReleases > 0, $"Evaluated={result.EvaluatedReleases}, Protected={result.ProtectedReleases}, Pruned={result.PrunedReleases}");

        // Verificar en base de datos PostgreSQL
        await factory.ExecuteDbContextAsync(async db =>
        {
            var dbReleases = await db.MobileReleases
                .Where(r => r.Channel == testChannel)
                .ToListAsync();

            var stable = dbReleases.First(r => r.Id == releases[0].Id);
            Assert.True(stable.IsCurrentStable);
            Assert.NotEqual(MobileReleaseStatuses.Pruned, stable.Status);
            Assert.True(await storage.ExistsAsync(stable.ArtifactFileName));

            var lastGood = dbReleases.First(r => r.Id == releases[1].Id);
            Assert.True(lastGood.IsLastGood);
            Assert.NotEqual(MobileReleaseStatuses.Pruned, lastGood.Status);
            Assert.True(await storage.ExistsAsync(lastGood.ArtifactFileName));

            var underInv = dbReleases.First(r => r.Id == releases[2].Id);
            Assert.Equal(MobileReleaseStatuses.UnderInvestigation, underInv.Status);
            Assert.True(await storage.ExistsAsync(underInv.ArtifactFileName));

            // Verificar que las podadas tienen status Pruned en BD pero el registro existe (conserva auditoría)
            var pruned = dbReleases.Where(r => r.Status == MobileReleaseStatuses.Pruned).ToList();
            Assert.NotEmpty(pruned);
            foreach (var p in pruned)
            {
                // El archivo físico fue eliminado
                Assert.False(await storage.ExistsAsync(p.ArtifactFileName));
            }
        });
    }

    private static byte[] CreateZipPackage(
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

            var apkEntry = archive.CreateEntry(apkFileName);
            using var apkStream = apkEntry.Open();
            apkStream.Write(apkContent, 0, apkContent.Length);
        }

        return memoryStream.ToArray();
    }
}
