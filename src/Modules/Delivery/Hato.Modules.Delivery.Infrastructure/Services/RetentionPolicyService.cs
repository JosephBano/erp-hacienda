using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hato.Modules.Delivery.Infrastructure.Services;

public class RetentionPolicyService(
    IDeliveryDbContext dbContext,
    IArtifactStorage artifactStorage,
    ILogger<RetentionPolicyService> logger) : IRetentionPolicyService
{
    private const int StageMaxCount = 10;
    private static readonly TimeSpan StageMaxAge = TimeSpan.FromDays(30);

    private const int ProdMaxCount = 5;
    private static readonly TimeSpan ProdMaxAge = TimeSpan.FromDays(180);

    public async Task<RetentionExecutionResult> ApplyRetentionAsync(
        string channel,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ejecutando política de retención para canal '{Channel}' (dryRun={DryRun})",
            channel, dryRun);

        var now = DateTimeOffset.UtcNow;
        var maxCount = channel == MobileReleaseChannels.Prod ? ProdMaxCount : StageMaxCount;
        var maxAge = channel == MobileReleaseChannels.Prod ? ProdMaxAge : StageMaxAge;
        var cutoffDate = now - maxAge;

        // Cargar todas las releases del canal que no estén ya podadas, ordenadas de más reciente a más antigua
        var releases = await dbContext.MobileReleases
            .Where(r => r.Channel == channel && r.Status != MobileReleaseStatuses.Pruned)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var evaluatedCount = releases.Count;
        var prunedIds = new List<Guid>();
        var protectedCount = 0;
        long bytesFreed = 0;

        int keptCount = 0;
        for (int i = 0; i < releases.Count; i++)
        {
            var release = releases[i];

            // 1. Verificación de protecciones estrictas
            if (release.IsCurrentStable || release.IsLastGood || release.Status == MobileReleaseStatuses.UnderInvestigation)
            {
                protectedCount++;
                keptCount++;
                continue;
            }

            // 2. Criterios de retención: si supera el conteo máximo O supera la antigüedad máxima
            bool exceedsCount = keptCount >= maxCount;
            bool exceedsAge = release.CreatedAt < cutoffDate;

            if (exceedsCount || exceedsAge)
            {
                prunedIds.Add(release.Id);
                bytesFreed += release.ArtifactSizeBytes;

                if (!dryRun)
                {
                    logger.LogInformation("Podando artefacto '{FileName}' de release {ReleaseId} (antigüedad={Age}d, posición={Pos})",
                        release.ArtifactFileName, release.Id, (now - release.CreatedAt).TotalDays, i + 1);

                    release.MarkPruned(reason: exceedsAge
                        ? $"Poda automática: excede antigüedad máxima de {maxAge.TotalDays} días"
                        : $"Poda automática: excede cuota de retención de {maxCount} artefactos");

                    await artifactStorage.DeleteIfExistsAsync(release.ArtifactFileName, cancellationToken);
                }
            }
            else
            {
                keptCount++;
            }
        }

        if (!dryRun && prunedIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Política de retención completada para canal '{Channel}': Evaluadas={Eval}, Podadas={Pruned}, Protegidas={Prot}, Liberado={Bytes} B",
            channel, evaluatedCount, prunedIds.Count, protectedCount, bytesFreed);

        return new RetentionExecutionResult(
            channel,
            evaluatedCount,
            prunedIds.Count,
            protectedCount,
            bytesFreed,
            prunedIds);
    }
}
