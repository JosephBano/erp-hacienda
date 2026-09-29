using FluentValidation;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Application.DTOs;
using Hato.Modules.Delivery.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Application.Releases.Commands;

public record PublishReleaseCommand(
    Guid ReleaseId,
    Guid PublishedBy,
    string? Reason = null) : IRequest<MobileReleaseDto>;

public class PublishReleaseCommandValidator : AbstractValidator<PublishReleaseCommand>
{
    public PublishReleaseCommandValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty().WithMessage("El ID de release es obligatorio.");
        RuleFor(x => x.PublishedBy).NotEmpty().WithMessage("El usuario que publica es obligatorio.");
    }
}

public record WithdrawReleaseCommand(
    Guid ReleaseId,
    Guid WithdrawnBy,
    string Reason) : IRequest<MobileReleaseDto>;

public class WithdrawReleaseCommandValidator : AbstractValidator<WithdrawReleaseCommand>
{
    public WithdrawReleaseCommandValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty().WithMessage("El ID de release es obligatorio.");
        RuleFor(x => x.WithdrawnBy).NotEmpty().WithMessage("El usuario que retira es obligatorio.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de retiro es obligatorio.");
    }
}

public class ReleaseCommandsHandler(IDeliveryDbContext dbContext)
    : IRequestHandler<PublishReleaseCommand, MobileReleaseDto>,
      IRequestHandler<WithdrawReleaseCommand, MobileReleaseDto>
{
    public async Task<MobileReleaseDto> Handle(
        PublishReleaseCommand request,
        CancellationToken cancellationToken)
    {
        var release = await dbContext.MobileReleases
            .Include(r => r.TransitionAudits)
            .FirstOrDefaultAsync(r => r.Id == request.ReleaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Release con ID '{request.ReleaseId}' no encontrada.");

        // Demote existing current stable for this channel
        var currentStable = await dbContext.MobileReleases
            .Where(r => r.Channel == release.Channel && r.IsCurrentStable && r.Id != release.Id)
            .ToListAsync(cancellationToken);

        foreach (var stable in currentStable)
        {
            stable.DemoteCurrentStable();
        }

        release.Publish(request.PublishedBy, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(release);
    }

    public async Task<MobileReleaseDto> Handle(
        WithdrawReleaseCommand request,
        CancellationToken cancellationToken)
    {
        var release = await dbContext.MobileReleases
            .Include(r => r.TransitionAudits)
            .FirstOrDefaultAsync(r => r.Id == request.ReleaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Release con ID '{request.ReleaseId}' no encontrada.");

        release.Withdraw(request.WithdrawnBy, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(release);
    }

    internal static MobileReleaseDto MapToDto(MobileRelease r) =>
        new(
            r.Id,
            r.BuildRequestId,
            r.Channel,
            r.Version,
            r.VersionCode,
            r.PackageName,
            r.TargetApiUrl,
            r.CommitSha,
            r.ReleaseTag,
            r.ArtifactFileName,
            r.ArtifactSha256,
            r.ArtifactSizeBytes,
            r.SigningCertificateFingerprint,
            r.Status,
            r.IsCurrentStable,
            r.IsLastGood,
            r.Notes,
            r.ApiCompatibility,
            r.PublishedAt,
            r.PublishedBy,
            r.WithdrawnAt,
            r.WithdrawnBy,
            r.WithdrawReason,
            r.CreatedAt,
            r.UpdatedAt,
            r.TransitionAudits.Select(a => new MobileReleaseTransitionAuditDto(
                a.Id, a.FromStatus, a.ToStatus, a.ChangedAt, a.ChangedBy, a.Reason)).ToList());
}
