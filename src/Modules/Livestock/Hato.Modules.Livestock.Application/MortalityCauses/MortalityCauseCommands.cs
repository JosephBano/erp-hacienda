using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.MortalityCauses;

public record CreateMortalityCauseCommand(string Name) : IRequest<Guid>;

public class CreateMortalityCauseValidator : AbstractValidator<CreateMortalityCauseCommand>
{
    public CreateMortalityCauseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateMortalityCauseHandler(ILivestockDbContext dbContext) : IRequestHandler<CreateMortalityCauseCommand, Guid>
{
    public async Task<Guid> Handle(CreateMortalityCauseCommand request, CancellationToken cancellationToken)
    {
        var cause = MortalityCause.Create(request.Name);

        dbContext.MortalityCauses.Add(cause);
        await dbContext.SaveChangesAsync(cancellationToken);

        return cause.Id;
    }
}

public record DeactivateMortalityCauseCommand(Guid Id) : IRequest;

public class DeactivateMortalityCauseHandler(ILivestockDbContext dbContext) : IRequestHandler<DeactivateMortalityCauseCommand>
{
    public async Task Handle(DeactivateMortalityCauseCommand request, CancellationToken cancellationToken)
    {
        var cause = await dbContext.MortalityCauses.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (cause is null)
            throw new DomainException($"La causa de mortalidad con ID '{request.Id}' no existe.");

        cause.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
