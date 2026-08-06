using Hato.Modules.People.Domain;
using Hato.Modules.People.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Users;

public record DeactivateUserCommand(Guid UserId) : IRequest;

public class DeactivateUserCommandHandler(IPeopleDbContext dbContext) : IRequestHandler<DeactivateUserCommand>
{
    private static readonly SemaphoreSlim AdminDeactivationLock = new(1, 1);

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new DomainException($"El usuario con ID '{request.UserId}' no existe.");

        var isAdmin = user.HasRole(SystemRoles.Admin);

        if (!isAdmin)
        {
            user.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await AdminDeactivationLock.WaitAsync(cancellationToken);
        try
        {
            var otherActiveAdmins = await dbContext.Users.CountAsync(
                u => u.IsActive && u.Id != user.Id && u.UserRoles.Any(ur => ur.Role.Code == SystemRoles.Admin),
                cancellationToken);

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
