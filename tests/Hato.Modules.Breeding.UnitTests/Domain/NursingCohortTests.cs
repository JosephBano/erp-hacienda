using Hato.Modules.Breeding.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Breeding.UnitTests.Domain;

public class NursingCohortTests
{
    private readonly Guid _speciesId = Guid.NewGuid();

    [Fact]
    public void Open_WithFirstBirthDate_StoresSpeciesAndStartDate()
    {
        var startDate = new DateOnly(2026, 8, 1);

        var cohort = NursingCohort.Open(_speciesId, startDate, notes: "Camada inicial");

        Assert.Equal(_speciesId, cohort.SpeciesId);
        Assert.Equal(startDate, cohort.StartedAt);
        Assert.Null(cohort.ClosedAt);
        Assert.Null(cohort.WeanedAt);
        Assert.Equal("Camada inicial", cohort.Notes);
    }

    [Fact]
    public void Open_EmptySpecies_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            NursingCohort.Open(Guid.Empty, new DateOnly(2026, 8, 1)));

        Assert.Contains("especie", ex.Message);
    }

    [Fact]
    public void Open_DefaultDate_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            NursingCohort.Open(_speciesId, default));

        Assert.Contains("fecha de inicio", ex.Message);
    }

    [Fact]
    public void ComputeWeaningDate_AddsLactationDaysFromLatestBirth()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1));
        var latestBirth = new DateOnly(2026, 8, 4);

        var weaningDate = cohort.ComputeWeaningDate(daysOfLactation: 24, latestBirthDate: latestBirth);

        Assert.Equal(new DateOnly(2026, 8, 28), weaningDate);
    }

    [Fact]
    public void ComputeWeaningDate_ZeroOrNegativeDays_ThrowsDomainException()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1));

        Assert.Throws<DomainException>(() =>
            cohort.ComputeWeaningDate(daysOfLactation: 0, latestBirthDate: new DateOnly(2026, 8, 1)));
        Assert.Throws<DomainException>(() =>
            cohort.ComputeWeaningDate(daysOfLactation: -1, latestBirthDate: new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void ComputeWeaningDate_LatestBirthBeforeStart_ThrowsDomainException()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 5));

        var ex = Assert.Throws<DomainException>(() =>
            cohort.ComputeWeaningDate(daysOfLactation: 24, latestBirthDate: new DateOnly(2026, 8, 1)));

        Assert.Contains("anterior", ex.Message);
    }

    [Fact]
    public void RecordWeaning_SetsClosedAtAndWeanedAt()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1));
        var weaningDate = new DateOnly(2026, 8, 28);

        cohort.RecordWeaning(weaningDate, weanedCount: 24, notes: "Mix listo");

        Assert.Equal(weaningDate, cohort.ClosedAt);
        Assert.Equal(weaningDate, cohort.WeanedAt);
        Assert.Equal("Mix listo", cohort.Notes);
    }

    [Fact]
    public void RecordWeaning_AppendsNotesWhenAlreadyPresent()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1), notes: "Inicial");
        var weaningDate = new DateOnly(2026, 8, 28);

        cohort.RecordWeaning(weaningDate, weanedCount: 24, notes: "todo OK");

        Assert.Equal("Inicial | Destete: todo OK", cohort.Notes);
    }

    [Fact]
    public void RecordWeaning_Twice_ThrowsDomainException()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1));
        cohort.RecordWeaning(new DateOnly(2026, 8, 28), weanedCount: 24);

        Assert.Throws<DomainException>(() =>
            cohort.RecordWeaning(new DateOnly(2026, 8, 29), weanedCount: 24));
    }

    [Fact]
    public void RecordWeaning_BeforeStart_ThrowsDomainException()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 5));

        var ex = Assert.Throws<DomainException>(() =>
            cohort.RecordWeaning(new DateOnly(2026, 8, 1), weanedCount: 0));

        Assert.Contains("anterior", ex.Message);
    }

    [Fact]
    public void RecordWeaning_NegativeWeanedCount_ThrowsDomainException()
    {
        var cohort = NursingCohort.Open(_speciesId, new DateOnly(2026, 8, 1));

        var ex = Assert.Throws<DomainException>(() =>
            cohort.RecordWeaning(new DateOnly(2026, 8, 28), weanedCount: -1));

        Assert.Contains("negativa", ex.Message);
    }
}
