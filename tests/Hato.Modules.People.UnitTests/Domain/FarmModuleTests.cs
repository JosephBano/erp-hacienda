using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.People.UnitTests.Domain;

public class FarmModuleTests
{
    [Fact]
    public void Create_DefaultsToEnabled()
    {
        var module = FarmModule.Create("production");

        Assert.Equal("production", module.Key);
        Assert.True(module.Enabled);
        Assert.Null(module.DisabledReason);
    }

    [Fact]
    public void Create_EmptyKey_Throws()
    {
        Assert.Throws<DomainException>(() => FarmModule.Create(""));
    }

    [Fact]
    public void Create_NormalisesKeyToLowercase()
    {
        var module = FarmModule.Create("Production");
        Assert.Equal("production", module.Key);
    }

    [Fact]
    public void SetEnabled_Disable_RequiresReason()
    {
        var module = FarmModule.Create("production");

        Assert.Throws<DomainException>(() => module.SetEnabled(false));
    }

    [Fact]
    public void SetEnabled_Disable_StoresReason()
    {
        var module = FarmModule.Create("production");

        module.SetEnabled(false, "no aplica al piloto");

        Assert.False(module.Enabled);
        Assert.Equal("no aplica al piloto", module.DisabledReason);
    }

    [Fact]
    public void SetEnabled_Enable_ClearsReason()
    {
        var module = FarmModule.Create("production", enabled: false, disabledReason: "x");

        module.SetEnabled(true, disabledReason: null);

        Assert.True(module.Enabled);
        Assert.Null(module.DisabledReason);
    }
}
