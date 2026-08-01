using FluentValidation;

namespace Hato.Modules.Livestock.Application.Animals;

public class AssignAnimalIdentifierValidator : AbstractValidator<AssignAnimalIdentifierCommand>
{
    public AssignAnimalIdentifierValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
    }
}
