using FluentValidation;

namespace Hato.Modules.Livestock.Application.Species;

public class CreateSpeciesValidator : AbstractValidator<CreateSpeciesCommand>
{
    public CreateSpeciesValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GestationDays).GreaterThan(0).When(x => x.GestationDays.HasValue);
        RuleFor(x => x.DaysOfLactation).GreaterThan(0).When(x => x.DaysOfLactation.HasValue);
        RuleFor(x => x.CohortWindowDays).GreaterThan(0).When(x => x.CohortWindowDays.HasValue);
    }
}
