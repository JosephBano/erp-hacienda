using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Application.DTOs;
using Hato.Modules.Delivery.Application.Releases.Commands;
using Hato.Modules.Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Application.Releases.Queries;

public record ListReleasesQuery(string? Channel = null, string? Status = null)
    : IRequest<IReadOnlyCollection<MobileReleaseDto>>;

public record GetReleaseByIdQuery(Guid Id) : IRequest<MobileReleaseDto?>;

public record DownloadReleaseResult(
    Stream ContentStream,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256);

public record DownloadReleaseQuery(Guid Id, bool HasManagePermission)
    : IRequest<DownloadReleaseResult>;

public class ReleaseQueriesHandler(
    IDeliveryDbContext dbContext,
    IArtifactStorage artifactStorage)
    : IRequestHandler<ListReleasesQuery, IReadOnlyCollection<MobileReleaseDto>>,
      IRequestHandler<GetReleaseByIdQuery, MobileReleaseDto?>,
      IRequestHandler<DownloadReleaseQuery, DownloadReleaseResult>
{
    public async Task<IReadOnlyCollection<MobileReleaseDto>> Handle(
        ListReleasesQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MobileReleases.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            var channel = request.Channel.Trim().ToLowerInvariant();
            query = query.Where(r => r.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(r => r.Status == status);
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return list.Select(ReleaseCommandsHandler.MapToDto).ToList();
    }

    public async Task<MobileReleaseDto?> Handle(
        GetReleaseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.MobileReleases
            .AsNoTracking()
            .Include(r => r.TransitionAudits)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        return entity is null ? null : ReleaseCommandsHandler.MapToDto(entity);
    }

    public async Task<DownloadReleaseResult> Handle(
        DownloadReleaseQuery request,
        CancellationToken cancellationToken)
    {
        var release = await dbContext.MobileReleases
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Release con ID '{request.Id}' no encontrada.");

        // Stage releases additionally require delivery.builds.manage to avoid exposing experimental builds to field operators
        if (release.Channel == MobileReleaseChannels.Stage && !request.HasManagePermission)
        {
            throw new UnauthorizedAccessException(
                "La descarga de artefactos del canal 'stage' requiere el permiso 'delivery.builds.manage'.");
        }

        var stream = await artifactStorage.OpenReadStreamAsync(release.ArtifactFileName, cancellationToken)
            ?? throw new FileNotFoundException(
                $"El archivo binario '{release.ArtifactFileName}' no fue encontrado en el almacenamiento.");

        return new DownloadReleaseResult(
            ContentStream: stream,
            FileName: release.ArtifactFileName,
            ContentType: "application/vnd.android.package-archive",
            SizeBytes: release.ArtifactSizeBytes,
            Sha256: release.ArtifactSha256);
    }
}
