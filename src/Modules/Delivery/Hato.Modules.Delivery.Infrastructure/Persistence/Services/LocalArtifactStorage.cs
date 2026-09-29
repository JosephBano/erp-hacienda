using Hato.Modules.Delivery.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Services;

public class LocalArtifactStorage : IArtifactStorage
{
    private readonly string _storagePath;

    public LocalArtifactStorage(IConfiguration configuration)
    {
        _storagePath = configuration["Delivery:ArtifactStoragePath"]
            ?? "/srv/hato-production/mobile-artifacts";
    }

    public LocalArtifactStorage(string storagePath)
    {
        _storagePath = storagePath;
    }

    public string GetStoragePath() => _storagePath;

    public Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(fileName);
        return Task.FromResult(File.Exists(path));
    }

    public Task<Stream?> OpenReadStreamAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(fileName);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public async Task SaveArtifactAsync(string fileName, Stream contentStream, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(fileName);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{path}.partial-{Guid.NewGuid():N}";
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await contentStream.CopyToAsync(fileStream, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public Task<bool> DeleteIfExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = GetSafePath(fileName);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string GetSafePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Nombre de archivo inválido.", nameof(fileName));

        var baseFileName = Path.GetFileName(fileName);
        if (baseFileName != fileName || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            throw new ArgumentException("Intento de path traversal detectado en el nombre de archivo.", nameof(fileName));

        return Path.Combine(_storagePath, baseFileName);
    }
}
