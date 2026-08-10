using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class DoseKindTests
{
    [Fact]
    public void Create_WithValidKeyAndLabel_IsActiveByDefault()
    {
        var kind = DoseKind.Create("absolute", "Absoluta (cantidad fija)");

        Assert.Equal("absolute", kind.Key);
        Assert.Equal("Absoluta (cantidad fija)", kind.LabelEs);
        Assert.True(kind.IsActive);
    }

    [Fact]
    public void Create_WithEmptyKey_Throws()
    {
        Assert.Throws<DomainException>(() => DoseKind.Create("   ", "Absoluta"));
    }

    [Fact]
    public void Create_NormalisesKeyToLowerSnakeCase()
    {
        Assert.Throws<DomainException>(() => DoseKind.Create("Per Weight", "Por peso"));

        var kind = DoseKind.Create("PER_WEIGHT", "Por peso");
        Assert.Equal("per_weight", kind.Key);
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var kind = DoseKind.Create("per_head", "Por cabeza");

        kind.Deactivate();

        Assert.False(kind.IsActive);
        Assert.Throws<DomainException>(() => kind.Deactivate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var kind = DoseKind.Create("per_head", "Por cabeza");
        kind.Deactivate();

        kind.Activate();

        Assert.True(kind.IsActive);
    }

    [Fact]
    public void Keys_MatchTheSeededCatalogue()
    {
        // 3.5a.2-B task 1: exactly three forms, matched against the
        // SeedDoseKinds migration by key. DoseResolver switches on these.
        Assert.Equal("absolute", DoseKind.Keys.Absolute);
        Assert.Equal("per_weight", DoseKind.Keys.PerWeight);
        Assert.Equal("per_head", DoseKind.Keys.PerHead);
    }
}
