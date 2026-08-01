using FluentValidation;

namespace Hato.Modules.Livestock.Application.Animals;

public class RegisterAnimalValidator : AbstractValidator<RegisterAnimalCommand>
{
    public RegisterAnimalValidator()
    {
        RuleFor(x => x.SpeciesId).NotEmpty();
        RuleFor(x => x.Sex).IsInEnum();
        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.BirthDate.HasValue)
            .WithMessage("La fecha de nacimiento no puede ser futura.");
    }
}
