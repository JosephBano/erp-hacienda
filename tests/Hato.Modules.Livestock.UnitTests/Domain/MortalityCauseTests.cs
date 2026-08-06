using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class MortalityCauseTests
{
    [Fact]
    public void Create_WithValidName_IsActiveByDefault()
    {
        var cause = MortalityCause.Create("Aplastamiento");

        Assert.Equal("Aplastamiento", cause.Name);
        Assert.True(cause.IsActive);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<DomainException>(() => MortalityCause.Create("   "));
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var cause = MortalityCause.Create("Hernia");
        cause.Deactivate();

        Assert.False(cause.IsActive);
        Assert.Throws<DomainException>(() => cause.Deactivate());
    }

    [Fact]
    public void Activate_OnAlreadyActiveCause_Throws()
    {
        var cause = MortalityCause.Create("Diarrea");

        Assert.Throws<DomainException>(() => cause.Activate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var cause = MortalityCause.Create("Diarrea");
        cause.Deactivate();

        cause.Activate();

        Assert.True(cause.IsActive);
    }
}
