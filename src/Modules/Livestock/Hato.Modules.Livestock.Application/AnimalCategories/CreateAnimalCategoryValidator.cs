using FluentValidation;

namespace Hato.Modules.Livestock.Application.AnimalCategories;

public class CreateAnimalCategoryValidator : AbstractValidator<CreateAnimalCategoryCommand>
{
    public CreateAnimalCategoryValidator()
    {
        RuleFor(x => x.SpeciesId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
