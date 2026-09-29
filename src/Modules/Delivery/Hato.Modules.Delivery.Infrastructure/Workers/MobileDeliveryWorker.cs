using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hato.Modules.Delivery.Infrastructure.Workers;

public class MobileDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MobileDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MobileDeliveryWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en ciclo de procesamiento de MobileDeliveryWorker: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("MobileDeliveryWorker detenido.");
    }

    public async Task<int> ProcessCycleAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDeliveryDbContext>();
        var gitHubClient = scope.ServiceProvider.GetRequiredService<IGitHubActionsClient>();
        var artifactImporter = scope.ServiceProvider.GetRequiredService<IArtifactImporter>();

        var processedCount = 0;

        // 1. Despachar solicitudes en cola respetando concurrencia de 1 build por canal
        foreach (var channel in new[] { MobileReleaseChannels.Stage, MobileReleaseChannels.Prod })
        {
            var isBuilding = await dbContext.MobileBuildRequests
                .AnyAsync(r => r.Channel == channel && r.Status == MobileBuildStatuses.Building, cancellationToken);

            if (isBuilding)
            {
                // Ya hay una compilación en curso para este canal; respetar cuota
                continue;
            }

            var nextRequest = await dbContext.MobileBuildRequests
                .Where(r => r.Channel == channel && r.Status == MobileBuildStatuses.Queued)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextRequest != null)
            {
                logger.LogInformation("Despachando solicitud {Id} para canal {Channel}...", nextRequest.Id, channel);
                nextRequest.MarkBuilding();
                await dbContext.SaveChangesAsync(cancellationToken);

                var dispatched = await gitHubClient.DispatchWorkflowAsync(nextRequest, cancellationToken);
                if (!dispatched)
                {
                    nextRequest.MarkFailed("No se pudo iniciar el workflow de compilación en GitHub Actions.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                processedCount++;
            }
        }

        // 2. Monitorear e importar solicitudes en estado Building
        var activeRequests = await dbContext.MobileBuildRequests
            .Where(r => r.Status == MobileBuildStatuses.Building)
            .ToListAsync(cancellationToken);

        if (activeRequests.Count == 0)
        {
            return processedCount;
        }

        var workflowRuns = await gitHubClient.ListWorkflowRunsAsync("build-android.yml", cancellationToken);

        foreach (var request in activeRequests)
        {
            var startedTime = request.UpdatedAt ?? request.CreatedAt;

            // Validar timeout operativo (90 min)
            if (DateTimeOffset.UtcNow - startedTime > BuildTimeout)
            {
                logger.LogWarning("Solicitud {Id} excedió el timeout de 90 minutos. Marcando como fallida.", request.Id);
                request.MarkFailed("Timeout de compilación: la tarea excedió 90 minutos sin completar.");
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount++;
                continue;
            }

            // Buscar run correlacionado estrictamente por commit SHA y tiempo de inicio (rechazo de runs ajenos)
            var matchingRun = workflowRuns.FirstOrDefault(run =>
                string.Equals(run.HeadSha, request.CommitSha, StringComparison.OrdinalIgnoreCase) &&
                run.CreatedAt >= startedTime.AddMinutes(-10));

            if (matchingRun == null)
            {
                // Aún no aparece en la API o está en preparación
                continue;
            }

            if (!string.Equals(matchingRun.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                // Run aún en ejecución (in_progress, queued)
                continue;
            }

            processedCount++;

            if (!string.Equals(matchingRun.Conclusion, "success", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Run de GitHub Actions {RunId} para solicitud {RequestId} finalizó con estado '{Conclusion}'.",
                    matchingRun.Id, request.Id, matchingRun.Conclusion);

                request.MarkFailed($"El workflow en GitHub Actions finalizó con estado '{matchingRun.Conclusion}'.");
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            // Run completado exitosamente: descargar artefacto e importar
            logger.LogInformation("Run {RunId} exitoso para solicitud {RequestId}. Descargando artefacto...",
                matchingRun.Id, request.Id);

            var artifactName = $"android-release-{request.Channel}-{request.VersionCode}";
            var zipStream = await gitHubClient.DownloadArtifactAsync(matchingRun.Id, artifactName, cancellationToken);

            if (zipStream == null)
            {
                logger.LogError("Artefacto '{Name}' no encontrado para run {RunId}", artifactName, matchingRun.Id);
                request.MarkFailed($"El artefacto '{artifactName}' no fue encontrado en los resultados del workflow.");
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                MobileRelease release;
                await using (zipStream)
                {
                    release = await artifactImporter.ImportArtifactAsync(request, zipStream, cancellationToken);
                }

                request.SetWorkflowRun(matchingRun.Id, matchingRun.RunAttempt);
                request.MarkCandidate(release.Id);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Solicitud {RequestId} completada exitosamente.", request.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo al importar artefacto para solicitud {RequestId}: {Message}",
                    request.Id, ex.Message);

                request.MarkFailed($"Error durante la importación del artefacto: {ex.Message}");
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return processedCount;
    }
}
