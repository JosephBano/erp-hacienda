using FluentValidation;

namespace Hato.Modules.Livestock.Application.Species;

public class CreateSpeciesValidator : AbstractValidator<CreateSpeciesCommand>
{
    public CreateSpeciesValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GestationDays).GreaterThan(0).When(x => x.GestationDays.HasValue);
    }
}
