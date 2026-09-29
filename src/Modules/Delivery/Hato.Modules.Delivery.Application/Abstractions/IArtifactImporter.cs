using Hato.Modules.Delivery.Domain;

namespace Hato.Modules.Delivery.Application.Abstractions;

public interface IArtifactImporter
{
    Task<MobileRelease> ImportArtifactAsync(
        MobileBuildRequest request,
        Stream artifactZipStream,
        CancellationToken cancellationToken = default);
}
