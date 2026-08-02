using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class BreedTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var speciesId = Guid.NewGuid();

        var breed = Breed.Create(speciesId, "Holstein");

        Assert.Equal(speciesId, breed.SpeciesId);
        Assert.Equal("Holstein", breed.Name);
    }

    [Fact]
    public void Create_WithoutSpecies_Throws()
    {
        Assert.Throws<DomainException>(() => Breed.Create(Guid.Empty, "Holstein"));
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.Throws<DomainException>(() => Breed.Create(Guid.NewGuid(), " "));
    }
}
