using Hato.Modules.People.Domain;
using Hato.Modules.People.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Users;

public record DeactivateUserCommand(Guid UserId) : IRequest;

public class DeactivateUserCommandHandler(IPeopleDbContext dbContext) : IRequestHandler<DeactivateUserCommand>
{
    // Guards the "at least one active Admin remains" invariant. A plain
    // count-then-save has the same TOCTOU shape as the bootstrap race: two Admins
    // deactivating each other at the same time could both pass the "someone else is
    // still active" check before either SaveChanges commits, leaving zero Admins.
    // Process-local is sufficient — this API runs as a single instance.
    private static readonly SemaphoreSlim AdminDeactivationLock = new(1, 1);

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new DomainException($"El usuario con ID '{request.UserId}' no existe.");

        if (user.Role != UserRole.Admin)
        {
            user.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await AdminDeactivationLock.WaitAsync(cancellationToken);
        try
        {
            var otherActiveAdmins = await dbContext.Users.CountAsync(
                u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, cancellationToken);

            if (otherActiveAdmins == 0)
                throw new DomainException(
                    "No se puede desactivar al único Administrador activo: nadie más podría gestionar usuarios.");

            user.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            AdminDeactivationLock.Release();
        }
    }
}
