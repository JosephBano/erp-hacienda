using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class FarmModuleKeyTests
{
    [Fact]
    public void Create_HierarchicalKeyWithWhitespaceAndUppercase_NormalizesCorrectly()
    {
        var module = FarmModule.Create("Inventory.X ");
        Assert.Equal("inventory.x", module.Key);
        Assert.Equal("inventory", module.ParentKey);
    }

    [Fact]
    public void Create_ValidHierarchicalKey_IsAccepted()
    {
        var module = FarmModule.Create("inventory.transformations");
        Assert.Equal("inventory.transformations", module.Key);
        Assert.Equal("inventory", module.ParentKey);
    }

    [Theory]
    [InlineData("inventory..x")]
    [InlineData(".inventory")]
    [InlineData("inventory.")]
    [InlineData("inv entory")]
    public void Create_InvalidKeys_ThrowsDomainException(string invalidKey)
    {
        Assert.Throws<DomainException>(() => FarmModule.Create(invalidKey));
    }

    [Fact]
    public void ParentKey_RootModule_ReturnsNull()
    {
        var module = FarmModule.Create("inventory");
        Assert.Null(module.ParentKey);
    }

    [Fact]
    public void ParentKey_MultiLevelModule_ReturnsImmediateParent()
    {
        var module = FarmModule.Create("a.b.c");
        Assert.Equal("a.b", module.ParentKey);
    }
}
