using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Domain.Exceptions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record ChangeAnimalGroupTrackingModeCommand(Guid Id, TrackingMode TrackingMode) : IRequest<AnimalGroupDto>;

public class ChangeAnimalGroupTrackingModeValidator : AbstractValidator<ChangeAnimalGroupTrackingModeCommand>
{
    public ChangeAnimalGroupTrackingModeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrackingMode).IsInEnum();
    }
}

public class ChangeAnimalGroupTrackingModeHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ChangeAnimalGroupTrackingModeCommand, AnimalGroupDto>
{
    public async Task<AnimalGroupDto> Handle(ChangeAnimalGroupTrackingModeCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        var isNoOp = request.TrackingMode == group.TrackingMode;

        if (!isNoOp)
        {
            // Domain invariant: throws DomainException → 400 if IsActive == false.
            // The message is owned by the domain; we let it bubble.
            group.ChangeTrackingMode(request.TrackingMode);

            // Application-level guards (ADR-0025 sec.3): the entity can't see these
            // tables without breaking Clean Architecture, so the handler owns the
            // checks. Both produce 409 via AnimalGroupStateException.
            if (await dbContext.GroupMemberships.AnyAsync(m => m.GroupId == request.Id && m.LeftAt == null, cancellationToken))
            {
                throw new AnimalGroupStateException(
                    "No se puede cambiar el modo de seguimiento: el grupo ya tiene miembros activos. Cree un grupo nuevo.");
            }

            if (await dbContext.AnimalEvents.AnyAsync(e => e.GroupId == request.Id, cancellationToken))
            {
                throw new AnimalGroupStateException(
                    "No se puede cambiar el modo de seguimiento: el grupo ya tiene eventos registrados. Cree un grupo nuevo.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var speciesName = group.SpeciesId.HasValue
            ? await dbContext.Species
                .Where(s => s.Id == group.SpeciesId.Value)
                .Select(s => (string?)s.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var liveHeadCount = await GetAnimalGroupByIdHandler.ComputeLiveHeadCountAsync(
            dbContext, group.Id, group.Memberships.Count(m => m.IsActive), cancellationToken);

        return new AnimalGroupDto(
            group.Id, group.Name, group.Description, group.SpeciesId, speciesName,
            group.IsActive, group.TrackingMode, liveHeadCount,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
    }
}
