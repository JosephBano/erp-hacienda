using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record CreateAnimalGroupCommand(string Name, string? Description, Guid? SpeciesId) : IRequest<Guid>;

public class CreateAnimalGroupValidator : AbstractValidator<CreateAnimalGroupCommand>
{
    public CreateAnimalGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class CreateAnimalGroupHandler(ILivestockDbContext dbContext) : IRequestHandler<CreateAnimalGroupCommand, Guid>
{
    public async Task<Guid> Handle(CreateAnimalGroupCommand request, CancellationToken cancellationToken)
    {
        var group = AnimalGroup.Create(request.Name, request.Description, request.SpeciesId);

        dbContext.AnimalGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return group.Id;
    }
}
