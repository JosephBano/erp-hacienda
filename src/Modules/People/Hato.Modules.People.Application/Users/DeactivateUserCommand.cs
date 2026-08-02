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

        user.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
