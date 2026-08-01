using Hato.Modules.Breeding.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Breeding.UnitTests.Domain;

public class SemenStrawTests
{
    [Fact]
    public void Create_ValidData_CreatesStraw()
    {
        var breedId = Guid.NewGuid();
        var straw = SemenStraw.Create("STR-001", "Toro Brahman 500", breedId, 10, "BULL-999", "Alta Genetics");

        Assert.NotNull(straw);
        Assert.Equal("STR-001", straw.Code);
        Assert.Equal("Toro Brahman 500", straw.BullName);
        Assert.Equal(10, straw.InitialQuantity);
        Assert.Equal(10, straw.CurrentQuantity);
    }

    [Fact]
    public void UseStraw_DecrementsQuantity()
    {
        var straw = SemenStraw.Create("STR-001", "Toro Brahman 500", Guid.NewGuid(), 2);
        straw.UseStraw();

        Assert.Equal(1, straw.CurrentQuantity);
    }

    [Fact]
    public void UseStraw_Exhausted_ThrowsDomainException()
    {
        var straw = SemenStraw.Create("STR-001", "Toro Brahman 500", Guid.NewGuid(), 1);
        straw.UseStraw();

        var ex = Assert.Throws<DomainException>(() => straw.UseStraw());
        Assert.Contains("No remaining straws", ex.Message);
    }
}
