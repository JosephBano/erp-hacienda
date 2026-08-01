using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Breeding.UnitTests.Domain;

public class PregnancyTests
{
    [Fact]
    public void Start_BovineGestation_CalculatesExpectedBirthDate()
    {
        var damId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 1, 1);
        int bovineGestationDays = 283;
        var confirmedAt = new DateOnly(2026, 2, 15);

        var pregnancy = Pregnancy.Start(damId, Guid.NewGuid(), serviceDate, bovineGestationDays, confirmedAt);

        Assert.NotNull(pregnancy);
        Assert.Equal(PregnancyStatus.Active, pregnancy.Status);
        Assert.Equal(serviceDate.AddDays(283), pregnancy.ExpectedBirthDate);
    }

    [Fact]
    public void Start_PorcineGestation_CalculatesExpectedBirthDate()
    {
        var damId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 1, 1);
        int porcineGestationDays = 114;
        var confirmedAt = new DateOnly(2026, 1, 30);

        var pregnancy = Pregnancy.Start(damId, Guid.NewGuid(), serviceDate, porcineGestationDays, confirmedAt);

        Assert.Equal(serviceDate.AddDays(114), pregnancy.ExpectedBirthDate);
    }

    [Fact]
    public void MarkAborted_ActivePregnancy_ChangesStatusToAborted()
    {
        var pregnancy = Pregnancy.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), 283, new DateOnly(2026, 2, 1));
        pregnancy.MarkAborted(new DateOnly(2026, 3, 1), "High fever");

        Assert.Equal(PregnancyStatus.Aborted, pregnancy.Status);
    }
}
