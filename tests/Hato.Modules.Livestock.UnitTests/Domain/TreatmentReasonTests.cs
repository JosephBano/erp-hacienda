using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class TreatmentReasonTests
{
    [Fact]
    public void Create_WithValidKeyAndLabel_IsActiveByDefault()
    {
        var reason = TreatmentReason.Create("scheduled", "Programada (cronograma)");

        Assert.Equal("scheduled", reason.Key);
        Assert.Equal("Programada (cronograma)", reason.LabelEs);
        Assert.True(reason.IsActive);
    }

    [Fact]
    public void Create_WithEmptyKey_Throws()
    {
        Assert.Throws<DomainException>(() => TreatmentReason.Create("   ", "Programada"));
    }

    [Fact]
    public void Create_WithEmptyLabel_Throws()
    {
        Assert.Throws<DomainException>(() => TreatmentReason.Create("scheduled", "   "));
    }

    [Fact]
    public void Create_NormalisesKeyToLowerSnakeCase()
    {
        // Whitespace and non-snake characters throw — the key is the wire
        // identifier, so a typo in the panel cannot produce two reasons that
        // look the same to a human and different to a parser.
        Assert.Throws<DomainException>(() => TreatmentReason.Create("curative care", "Curativa"));
        Assert.Throws<DomainException>(() => TreatmentReason.Create("curative-care", "Curativa"));

        // Upper-case is normalised silently — same rule as the routes
        // catalog so the panel and the API do not fight over casing.
        var reason = TreatmentReason.Create("CURATIVE", "Curativa");
        Assert.Equal("curative", reason.Key);
    }

    [Fact]
    public void Create_AcceptsTheThreeFoundationalReasons()
    {
        // The plan seeds exactly these three (docs/planes/fase-3-5/sub-planes/3.5a.2-A.md):
        // distinguishing "tocaba por cronograma" from "curé algo" from
        // "preventivo fuera de cronograma" is the point of this catalogue.
        Assert.NotNull(TreatmentReason.Create("scheduled", "Programada (cronograma)"));
        Assert.NotNull(TreatmentReason.Create("curative", "Curativa"));
        Assert.NotNull(TreatmentReason.Create("preventive", "Preventiva"));
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var reason = TreatmentReason.Create("curative", "Curativa");
        reason.Deactivate();

        Assert.False(reason.IsActive);
        Assert.Throws<DomainException>(() => reason.Deactivate());
    }

    [Fact]
    public void Activate_OnAlreadyActiveReason_Throws()
    {
        var reason = TreatmentReason.Create("preventive", "Preventiva");

        Assert.Throws<DomainException>(() => reason.Activate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var reason = TreatmentReason.Create("curative", "Curativa");
        reason.Deactivate();

        reason.Activate();

        Assert.True(reason.IsActive);
    }

    [Fact]
    public void UpdateLabel_ChangesLabelOnly()
    {
        var reason = TreatmentReason.Create("scheduled", "Programada");
        reason.UpdateLabel("Programada por cronograma");

        Assert.Equal("Programada por cronograma", reason.LabelEs);
        Assert.Equal("scheduled", reason.Key);
    }
}