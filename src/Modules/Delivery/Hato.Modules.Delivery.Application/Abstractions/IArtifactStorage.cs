namespace Hato.Modules.Delivery.Application.Abstractions;

public interface IArtifactStorage
{
    Task<Stream?> OpenReadStreamAsync(string fileName, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default);
    Task SaveArtifactAsync(string fileName, Stream contentStream, CancellationToken cancellationToken = default);
    Task<bool> DeleteIfExistsAsync(string fileName, CancellationToken cancellationToken = default);
    Task<long> GetTotalStorageBytesAsync(CancellationToken cancellationToken = default);
    string GetStoragePath();
}
