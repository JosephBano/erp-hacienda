using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Domain.Exceptions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

/// <summary>
/// Returns <see cref="MediatR.Unit"/>: the endpoint exposes 204 NoContent. The full
/// <see cref="AnimalGroupDto"/> is what callers actually want after a successful change,
/// but the panel reloads the detail page on success and asks the server for the
/// canonical state via <c>GetAnimalGroupByIdQuery</c>, so we don't duplicate the
/// projection here. See PR3 review finding #7.
/// </summary>
public record ChangeAnimalGroupTrackingModeCommand(Guid Id, TrackingMode TrackingMode) : IRequest<MediatR.Unit>;

public class ChangeAnimalGroupTrackingModeValidator : AbstractValidator<ChangeAnimalGroupTrackingModeCommand>
{
    public ChangeAnimalGroupTrackingModeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TrackingMode).IsInEnum();
    }
}

public class ChangeAnimalGroupTrackingModeHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ChangeAnimalGroupTrackingModeCommand, MediatR.Unit>
{
    public async Task<MediatR.Unit> Handle(ChangeAnimalGroupTrackingModeCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        if (request.TrackingMode == group.TrackingMode)
        {
            // No-op: nothing to validate, nothing to persist. Avoids touching the DB
            // for a request that's effectively a GET.
            return MediatR.Unit.Value;
        }

        // Application-level guards FIRST (ADR-0025 sec.3). The entity cannot see these
        // tables without breaking Clean Architecture, so the handler owns the checks.
        // Both produce 409 via AnimalGroupStateException. Doing the guards before
        // mutating the entity keeps the in-memory state consistent if a future refactor
        // adds an implicit SaveChanges before this point — a defence-in-depth ordering
        // fix from PR3 review finding #1.
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

        // Domain invariant: throws DomainException → 400 if IsActive == false. The
        // message is owned by the domain; we let it bubble. Guards above already
        // verified no memberships and no events exist, so the only failure mode is
        // "inactive group".
        group.ChangeTrackingMode(request.TrackingMode);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MediatR.Unit.Value;
    }
}
