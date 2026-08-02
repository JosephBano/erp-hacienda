using Hato.Modules.People.Domain;
using Hato.Modules.People.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.Users;

public record DeactivateUserCommand(Guid UserId) : IRequest;

public class DeactivateUserCommandHandler(IPeopleDbContext dbContext) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new DomainException($"El usuario con ID '{request.UserId}' no existe.");

        if (user.Role == UserRole.Admin)
        {
            var otherActiveAdmins = await dbContext.Users.CountAsync(
                u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, cancellationToken);

            if (otherActiveAdmins == 0)
                throw new DomainException(
                    "No se puede desactivar al único Administrador activo: nadie más podría gestionar usuarios.");
        }

        user.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
