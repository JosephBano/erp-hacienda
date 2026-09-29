using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.Delivery.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Hato.Modules.Delivery.Infrastructure.Services;

public class ArtifactImporter(
    IDeliveryDbContext dbContext,
    IArtifactStorage artifactStorage,
    IRetentionPolicyService retentionPolicyService,
    IExternalArtifactUploader externalUploader,
    ILogger<ArtifactImporter> logger) : IArtifactImporter
{
    private static readonly Regex ApkFileNameRegex = new(
        @"^hato-[0-9]+\.[0-9]+\.[0-9]+-(stage|prod)-b[0-9]+-[a-f0-9]{7,40}\.apk$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const long MaxStorageBudgetBytes = 5L * 1024 * 1024 * 1024; // 5 GiB

    public async Task<MobileRelease> ImportArtifactAsync(
        MobileBuildRequest request,
        Stream artifactZipStream,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Iniciando importación de artefacto para solicitud {RequestId}, canal {Channel}",
            request.Id, request.Channel);

        using var zipArchive = new ZipArchive(artifactZipStream, ZipArchiveMode.Read, leaveOpen: true);

        // 1. Buscar y leer manifest.json
        var manifestEntry = zipArchive.GetEntry("manifest.json")
            ?? throw new ArtifactIntegrityException("El artefacto descargado no contiene 'manifest.json'.");

        ArtifactManifestDto manifest;
        try
        {
            await using var manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<ArtifactManifestDto>(
                manifestStream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken)
                ?? throw new ArtifactIntegrityException("El archivo manifest.json está vacío o es inválido.");
        }
        catch (JsonException ex)
        {
            throw new ArtifactIntegrityException($"Error al deserializar manifest.json: {ex.Message}");
        }

        // 2. Validar seguridad de ruta (Path Traversal Prevention)
        var fileName = manifest.ArtifactFileName?.Trim() ?? string.Empty;
        ValidateFileNameSecurity(fileName);

        // 3. Validar correlación con la solicitud
        if (!string.Equals(manifest.CommitSha?.Trim(), request.CommitSha.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArtifactIntegrityException(
                $"El commit SHA del artefacto ('{manifest.CommitSha}') no coincide con la solicitud ('{request.CommitSha}').");
        }

        if (manifest.VersionCode != request.VersionCode)
        {
            throw new ArtifactIntegrityException(
                $"El versionCode del artefacto ({manifest.VersionCode}) no coincide con el reservado ({request.VersionCode}).");
        }

        var expectedPackage = request.Channel == MobileReleaseChannels.Prod
            ? "com.joemandev.hatofieldapp"
            : "com.joemandev.hatofieldapp.stage";

        if (!string.Equals(manifest.PackageName?.Trim(), expectedPackage, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArtifactIntegrityException(
                $"El packageName del artefacto ('{manifest.PackageName}') no coincide con el esperado para el canal '{request.Channel}' ('{expectedPackage}').");
        }

        // 4. Buscar entrada del APK en el zip
        var apkEntry = zipArchive.GetEntry(fileName)
            ?? throw new ArtifactIntegrityException($"El archivo APK '{fileName}' no fue encontrado en el paquete ZIP.");

        // 5. Validar cuota de almacenamiento local (5 GiB)
        var currentStorageBytes = await artifactStorage.GetTotalStorageBytesAsync(cancellationToken);
        if (currentStorageBytes + manifest.ArtifactSizeBytes > MaxStorageBudgetBytes)
        {
            logger.LogWarning("Presupuesto de almacenamiento local excedido ({Current} + {New} > {Limit}). Aplicando retención...",
                currentStorageBytes, manifest.ArtifactSizeBytes, MaxStorageBudgetBytes);

            await retentionPolicyService.ApplyRetentionAsync(request.Channel, dryRun: false, cancellationToken);
            currentStorageBytes = await artifactStorage.GetTotalStorageBytesAsync(cancellationToken);

            if (currentStorageBytes + manifest.ArtifactSizeBytes > MaxStorageBudgetBytes)
            {
                throw new StorageQuotaExceededException(
                    $"El almacenamiento de artefactos ha alcanzado el límite presupuestado de 5 GiB. " +
                    $"Uso actual: {currentStorageBytes} B, necesario: {manifest.ArtifactSizeBytes} B. " +
                    "Importación bloqueada para proteger releases vigentes.");
            }
        }

        // 6. Extraer APK a memoria o stream, calcular SHA-256 y validar integridad
        byte[] apkBytes;
        string computedSha256;
        await using (var apkStream = apkEntry.Open())
        {
            using var memoryStream = new MemoryStream();
            await apkStream.CopyToAsync(memoryStream, cancellationToken);
            apkBytes = memoryStream.ToArray();

            var hashBytes = SHA256.HashData(apkBytes);
            computedSha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        if (apkBytes.Length != manifest.ArtifactSizeBytes)
        {
            throw new ArtifactIntegrityException(
                $"El tamaño real del APK ({apkBytes.Length} bytes) no coincide con el tamaño declarado en el manifest ({manifest.ArtifactSizeBytes} bytes).");
        }

        var expectedSha256 = manifest.Sha256?.Trim().ToLowerInvariant()
            ?? manifest.ArtifactSha256?.Trim().ToLowerInvariant()
            ?? string.Empty;

        if (!string.Equals(computedSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArtifactIntegrityException(
                $"El hash SHA-256 del APK ('{computedSha256}') no coincide con el manifest ('{expectedSha256}').");
        }

        // 7. Persistir APK en almacenamiento local seguro
        using (var saveStream = new MemoryStream(apkBytes))
        {
            await artifactStorage.SaveArtifactAsync(fileName, saveStream, cancellationToken);
        }

        // 8. Copia a Google Drive externa (namespace por canal)
        var localPath = Path.Combine(artifactStorage.GetStoragePath(), fileName);
        try
        {
            await externalUploader.UploadArtifactToDriveAsync(localPath, request.Channel, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no bloqueante al copiar artefacto a almacenamiento externo Drive: {Message}", ex.Message);
        }

        // 9. Registrar release candidata en la base de datos
        var release = MobileRelease.CreateCandidate(
            buildRequestId: request.Id,
            channel: request.Channel,
            version: manifest.Version?.Trim() ?? "1.0.0",
            versionCode: manifest.VersionCode,
            packageName: expectedPackage,
            targetApiUrl: manifest.TargetApiUrl?.Trim() ?? request.Channel switch
            {
                MobileReleaseChannels.Prod => "https://hato-oracle.ts.net/api",
                _ => "https://joemanserver.ts.net/api"
            },
            commitSha: request.CommitSha,
            artifactFileName: fileName,
            artifactSha256: computedSha256,
            artifactSizeBytes: apkBytes.Length,
            signingCertificateFingerprint: manifest.SigningCertificateFingerprint?.Trim() ?? "UNKNOWN",
            apiCompatibility: ">= 1.0.0",
            releaseTag: request.ReleaseTag,
            notes: $"Importado automáticamente por Delivery worker desde GitHub Actions run.");

        dbContext.MobileReleases.Add(release);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Artefacto '{FileName}' importado exitosamente como release {ReleaseId}",
            fileName, release.Id);

        return release;
    }

    private static void ValidateFileNameSecurity(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DeliverySecurityException("El nombre del archivo de artefacto no puede estar vacío.");

        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('\0'))
            throw new DeliverySecurityException($"Intento de path traversal detectado en el nombre de artefacto: '{fileName}'");

        if (Path.GetFileName(fileName) != fileName)
            throw new DeliverySecurityException($"El nombre de archivo contiene rutas o caracteres no permitidos: '{fileName}'");

        if (!ApkFileNameRegex.IsMatch(fileName))
            throw new DeliverySecurityException($"El nombre del artefacto no cumple con la nomenclatura esperada 'hato-<ver>-<canal>-b<codeSha>.apk': '{fileName}'");
    }

    private sealed class ArtifactManifestDto
    {
        [JsonPropertyName("artifact_file_name")]
        public string? ArtifactFileName { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("artifact_sha256")]
        public string? ArtifactSha256 { get; set; }

        [JsonPropertyName("artifact_size_bytes")]
        public long ArtifactSizeBytes { get; set; }

        [JsonPropertyName("commit_sha")]
        public string? CommitSha { get; set; }

        [JsonPropertyName("version_code")]
        public int VersionCode { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("package_name")]
        public string? PackageName { get; set; }

        [JsonPropertyName("target_api_url")]
        public string? TargetApiUrl { get; set; }

        [JsonPropertyName("signing_certificate_fingerprint")]
        public string? SigningCertificateFingerprint { get; set; }
    }
}
