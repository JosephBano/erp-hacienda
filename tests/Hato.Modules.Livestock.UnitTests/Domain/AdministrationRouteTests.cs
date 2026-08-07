using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class AdministrationRouteTests
{
    [Fact]
    public void Create_WithValidKeyAndLabel_IsActiveByDefault()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular (IM)");

        Assert.Equal("im", route.Key);
        Assert.Equal("Intramuscular (IM)", route.LabelEs);
        Assert.True(route.IsActive);
        Assert.Null(route.DefaultUnitId);
    }

    [Fact]
    public void Create_WithEmptyKey_Throws()
    {
        Assert.Throws<DomainException>(() => AdministrationRoute.Create("   ", "Oral en agua"));
    }

    [Fact]
    public void Create_WithEmptyLabel_Throws()
    {
        Assert.Throws<DomainException>(() => AdministrationRoute.Create("oral_water", "   "));
    }

    [Fact]
    public void Create_TrimsKeyAndLabel()
    {
        var route = AdministrationRoute.Create("  sc  ", "  Subcutánea  ");

        Assert.Equal("sc", route.Key);
        Assert.Equal("Subcutánea", route.LabelEs);
    }

    [Fact]
    public void Create_NormalisesKeyToLowerSnakeCase()
    {
        // The key is what the wire-format and sync pull use, so it has to be
        // stable. We reject whitespace inside the key and lowercase the rest.
        Assert.Throws<DomainException>(() => AdministrationRoute.Create("Oral Water", "Oral en agua"));
        Assert.Throws<DomainException>(() => AdministrationRoute.Create("oral water", "Oral en agua"));

        var route = AdministrationRoute.Create("ORAL_WATER", "Oral en agua");
        Assert.Equal("oral_water", route.Key);
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular");

        route.Deactivate();

        Assert.False(route.IsActive);
        Assert.Throws<DomainException>(() => route.Deactivate());
    }

    [Fact]
    public void Activate_OnAlreadyActiveRoute_Throws()
    {
        var route = AdministrationRoute.Create("sc", "Subcutánea");

        Assert.Throws<DomainException>(() => route.Activate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var route = AdministrationRoute.Create("topica", "Tópica");
        route.Deactivate();

        route.Activate();

        Assert.True(route.IsActive);
    }

    [Fact]
    public void UpdateLabel_ChangesLabelOnly()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular");

        route.UpdateLabel("Intramuscular (IM)");

        Assert.Equal("Intramuscular (IM)", route.LabelEs);
        Assert.Equal("im", route.Key);
        Assert.True(route.IsActive);
    }

    [Fact]
    public void UpdateLabel_WithEmpty_Throws()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular");

        Assert.Throws<DomainException>(() => route.UpdateLabel("   "));
    }

    [Fact]
    public void SetDefaultUnit_StoresUnitReference()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular");
        var unitId = Guid.NewGuid();

        route.SetDefaultUnit(unitId);

        Assert.Equal(unitId, route.DefaultUnitId);
    }

    [Fact]
    public void SetDefaultUnit_NullClearsTheReference()
    {
        var route = AdministrationRoute.Create("im", "Intramuscular");
        route.SetDefaultUnit(Guid.NewGuid());

        route.SetDefaultUnit(null);

        Assert.Null(route.DefaultUnitId);
    }
}