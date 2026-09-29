using System.Diagnostics;
using Hato.Modules.Delivery.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hato.Modules.Delivery.Infrastructure.Services;

public class ExternalArtifactUploader(
    IConfiguration configuration,
    ILogger<ExternalArtifactUploader> logger) : IExternalArtifactUploader
{
    public async Task<bool> UploadArtifactToDriveAsync(
        string localApkPath,
        string channel,
        CancellationToken cancellationToken = default)
    {
        var scriptPath = configuration["Delivery:BackupUploadScriptPath"] ?? "scripts/backup-upload.sh";
        var driveNamespace = $"mobile/{channel.ToLowerInvariant()}";

        if (!File.Exists(localApkPath))
        {
            logger.LogWarning("El archivo APK local '{Path}' no existe; omitiendo copia externa.", localApkPath);
            return false;
        }

        if (!File.Exists(scriptPath))
        {
            logger.LogInformation("Script de transporte a Drive ('{ScriptPath}') no encontrado en este entorno; copia simulada en namespace '{Namespace}'.",
                scriptPath, driveNamespace);
            return true;
        }

        logger.LogInformation("Ejecutando transporte externo a Drive para '{ApkPath}' en namespace '{Namespace}'...",
            localApkPath, driveNamespace);

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"\"{localApkPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processStartInfo.EnvironmentVariables["REMOTE_NAMESPACE"] = driveNamespace;

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogError("Fallo en copia a Drive (ExitCode {Code}): {Error}", process.ExitCode, stderr);
                return false;
            }

            logger.LogInformation("Copia externa a Drive completada con éxito en namespace '{Namespace}'.", driveNamespace);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al invocar transporte externo a Drive: {Message}", ex.Message);
            return false;
        }
    }
}
