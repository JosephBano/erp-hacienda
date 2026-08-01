using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class SpeciesTests
{
    [Fact]
    public void Create_WithValidName_Succeeds()
    {
        var species = Species.Create("Bovino", gestationDays: 283);

        Assert.Equal("Bovino", species.Name);
        Assert.Equal(283, species.GestationDays);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => Species.Create(name));
    }

    [Fact]
    public void Create_WithNonPositiveGestationDays_Throws()
    {
        Assert.Throws<DomainException>(() => Species.Create("Bovino", gestationDays: 0));
    }

    [Fact]
    public void Create_WithoutGestationDays_Succeeds()
    {
        var species = Species.Create("Equino");

        Assert.Null(species.GestationDays);
    }
}
