using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class AnimalCategoryTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var speciesId = Guid.NewGuid();

        var category = AnimalCategory.Create(speciesId, "Vaca en producción");

        Assert.Equal(speciesId, category.SpeciesId);
        Assert.Equal("Vaca en producción", category.Name);
    }

    [Fact]
    public void Create_WithoutSpecies_Throws()
    {
        Assert.Throws<DomainException>(() => AnimalCategory.Create(Guid.Empty, "Ternera"));
    }
}
