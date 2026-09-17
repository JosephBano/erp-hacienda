using Hato.Modules.Inventory.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Inventory.UnitTests;

public class FeedStageTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var stage = FeedStage.Create("pre_starter", "Preiniciador");

        Assert.NotEqual(Guid.Empty, stage.Id);
        Assert.Equal("pre_starter", stage.Key);
        Assert.Equal("Preiniciador", stage.LabelEs);
        Assert.True(stage.IsActive);
    }

    [Fact]
    public void Create_NormalisesKeyCase()
    {
        var stage = FeedStage.Create("  Gestation  ", "Gestación");

        Assert.Equal("gestation", stage.Key);
    }

    [Fact]
    public void Create_EmptyKey_Throws()
    {
        Assert.Throws<DomainException>(() => FeedStage.Create("", "Iniciador"));
    }

    [Fact]
    public void Create_EmptyLabel_Throws()
    {
        Assert.Throws<DomainException>(() => FeedStage.Create("starter", ""));
    }

    [Fact]
    public void Create_KeyWithWhitespaceInside_Throws()
    {
        Assert.Throws<DomainException>(() => FeedStage.Create("pre starter", "Preiniciador"));
    }

    [Fact]
    public void Create_KeyWithInvalidCharacters_Throws()
    {
        Assert.Throws<DomainException>(() => FeedStage.Create("crecimiento-1", "Crecimiento"));
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var stage = FeedStage.Create("finisher", "Engorde");

        stage.Deactivate();

        Assert.False(stage.IsActive);
        Assert.Throws<DomainException>(stage.Deactivate);
    }

    [Fact]
    public void Activate_AfterDeactivate_Succeeds()
    {
        var stage = FeedStage.Create("lactation", "Lactancia");
        stage.Deactivate();

        stage.Activate();

        Assert.True(stage.IsActive);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_Throws()
    {
        var stage = FeedStage.Create("grower", "Crecimiento");

        Assert.Throws<DomainException>(stage.Activate);
    }
}
