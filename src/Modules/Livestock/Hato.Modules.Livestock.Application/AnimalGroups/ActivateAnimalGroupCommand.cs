using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record ActivateAnimalGroupCommand(Guid Id) : IRequest<Unit>;

public class ActivateAnimalGroupValidator : AbstractValidator<ActivateAnimalGroupCommand>
{
    public ActivateAnimalGroupValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ActivateAnimalGroupHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ActivateAnimalGroupCommand, Unit>
{
    public async Task<Unit> Handle(ActivateAnimalGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"El grupo con ID '{request.Id}' no existe.");

        group.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
