namespace Hato.Modules.Delivery.Application.Abstractions;

public interface IExternalArtifactUploader
{
    Task<bool> UploadArtifactToDriveAsync(
        string localApkPath,
        string channel,
        CancellationToken cancellationToken = default);
}
