using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class TreatmentCourseTests
{
    private static TreatmentCourse CreateAnimalCourse(DateTimeOffset? startsAt = null) =>
        TreatmentCourse.CreateForAnimal(
            Guid.NewGuid(),
            startsAt ?? DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "curative",
            productId: null,
            Guid.NewGuid(),
            doseFactorAmount: 0.1m,
            doseFactorUnit: "ml",
            notes: null);

    [Fact]
    public void CreateForAnimal_WithValidData_Succeeds()
    {
        var animalId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var doseKindId = Guid.NewGuid();
        var startsAt = DateTimeOffset.UtcNow;

        var course = TreatmentCourse.CreateForAnimal(
            animalId, startsAt, routeId, "curative", null, doseKindId, 0.1m, "ml", "nota libre");

        Assert.Equal(animalId, course.AnimalId);
        Assert.Null(course.GroupId);
        Assert.Equal(routeId, course.RouteId);
        Assert.Equal("curative", course.Reason);
        Assert.Equal(doseKindId, course.DoseKindId);
        Assert.Equal(0.1m, course.DoseFactorAmount);
        Assert.Equal("ml", course.DoseFactorUnit);
        Assert.Equal(startsAt, course.StartsAt);
        Assert.Equal(startsAt, course.EndsAt);
        Assert.False(course.IsSynthetic);
        Assert.Empty(course.Applications);
    }

    [Fact]
    public void CreateForAnimal_WithEmptyAnimalId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.Empty, DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, Guid.NewGuid(), 1m, "ml", null));
    }

    [Fact]
    public void CreateForGroup_WithEmptyGroupId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForGroup(
                Guid.Empty, DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, Guid.NewGuid(), 1m, "ml", null));
    }

    [Fact]
    public void Create_WithEmptyRoute_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty, null, null, Guid.NewGuid(), 1m, "ml", null));
    }

    [Fact]
    public void Create_WithZeroOrNegativeFactor_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, Guid.NewGuid(), 0m, "ml", null));

        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, Guid.NewGuid(), -1m, "ml", null));
    }

    [Fact]
    public void Create_WithEmptyFactorUnit_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), null, null, Guid.NewGuid(), 1m, "  ", null));
    }

    [Fact]
    public void Create_WithUnknownReason_Throws()
    {
        Assert.Throws<DomainException>(() =>
            TreatmentCourse.CreateForAnimal(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "made_up_reason", null, Guid.NewGuid(), 1m, "ml", null));
    }

    [Fact]
    public void Create_NormalisesReasonCasing()
    {
        var course = TreatmentCourse.CreateForAnimal(
            Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "CURATIVE", null, Guid.NewGuid(), 1m, "ml", null);

        Assert.Equal("curative", course.Reason);
    }

    // --- Task 1: dose with a value but no unit is rejected; both null is fine. -----

    [Fact]
    public void AddApplication_AdministeredDoseWithoutUnit_Throws()
    {
        var course = CreateAnimalCourse();

        Assert.Throws<DomainException>(() => course.AddApplication(
            1, DateTimeOffset.UtcNow, null, null, false, administeredDoseAmount: 10m, administeredDoseUnit: null, notes: null));
    }

    [Fact]
    public void AddApplication_AdministeredDoseWithUnit_Succeeds()
    {
        var course = CreateAnimalCourse();

        var application = course.AddApplication(
            1, DateTimeOffset.UtcNow, null, null, false, administeredDoseAmount: 10m, administeredDoseUnit: "ml", notes: null);

        Assert.Equal(10m, application.AdministeredDoseAmount);
        Assert.Equal("ml", application.AdministeredDoseUnit);
    }

    [Fact]
    public void AddApplication_NullDoseWithOrWithoutNotes_Succeeds()
    {
        // Task 3 / test 2: an absent dose is a valid application (Art. 1) — the
        // narrative is recommended but not domain-enforced.
        var course = CreateAnimalCourse();

        var withNotes = course.AddApplication(
            1, DateTimeOffset.UtcNow, null, null, false, null, null, notes: "se derramó al aplicar");
        Assert.Null(withNotes.AdministeredDoseAmount);

        var course2 = CreateAnimalCourse();
        var withoutNotes = course2.AddApplication(1, DateTimeOffset.UtcNow, null, null, false, null, null, notes: null);
        Assert.Null(withoutNotes.AdministeredDoseAmount);
    }

    [Fact]
    public void AddApplication_CalculatedDoseCoupling_AlsoEnforced()
    {
        var course = CreateAnimalCourse();

        Assert.Throws<DomainException>(() => course.AddApplication(
            1, DateTimeOffset.UtcNow, calculatedDoseAmount: 20m, calculatedDoseUnit: null,
            isEstimated: false, administeredDoseAmount: null, administeredDoseUnit: null, notes: null));
    }

    // --- Task 5: EndsAt tracks the latest application; application numbers -----

    [Fact]
    public void AddApplication_MultipleDays_EndsAtTracksLatest()
    {
        var start = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var course = CreateAnimalCourse(start);

        course.AddApplication(1, start, null, null, false, null, null, null);
        course.AddApplication(2, start.AddDays(1), null, null, false, null, null, null);
        course.AddApplication(3, start.AddDays(2), null, null, false, null, null, null);

        Assert.Equal(3, course.Applications.Count);
        Assert.Equal(start.AddDays(2), course.EndsAt);
    }

    [Fact]
    public void AddApplication_DuplicateApplicationNo_Throws()
    {
        var course = CreateAnimalCourse();
        course.AddApplication(1, DateTimeOffset.UtcNow, null, null, false, null, null, null);

        Assert.Throws<DomainException>(() =>
            course.AddApplication(1, DateTimeOffset.UtcNow, null, null, false, null, null, null));
    }

    [Fact]
    public void AddApplication_NonPositiveApplicationNo_Throws()
    {
        var course = CreateAnimalCourse();

        Assert.Throws<DomainException>(() =>
            course.AddApplication(0, DateTimeOffset.UtcNow, null, null, false, null, null, null));
    }

    [Fact]
    public void AddApplication_CalculatedAndAdministeredDiffer_BothPersistWithoutReconciliation()
    {
        // Task 2 / test 6: the gap is data, never corrected.
        var course = CreateAnimalCourse();

        var application = course.AddApplication(
            1, DateTimeOffset.UtcNow,
            calculatedDoseAmount: 147m, calculatedDoseUnit: "ml", isEstimated: true,
            administeredDoseAmount: 200m, administeredDoseUnit: "ml", notes: null);

        Assert.Equal(147m, application.CalculatedDoseAmount);
        Assert.Equal(200m, application.AdministeredDoseAmount);
        Assert.True(application.IsEstimated);
    }
}
