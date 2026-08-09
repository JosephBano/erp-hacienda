using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record DeactivateAnimalGroupCommand(Guid Id) : IRequest<Unit>;

public class DeactivateAnimalGroupValidator : AbstractValidator<DeactivateAnimalGroupCommand>
{
    public DeactivateAnimalGroupValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeactivateAnimalGroupHandler(ILivestockDbContext dbContext)
    : IRequestHandler<DeactivateAnimalGroupCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateAnimalGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        group.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
