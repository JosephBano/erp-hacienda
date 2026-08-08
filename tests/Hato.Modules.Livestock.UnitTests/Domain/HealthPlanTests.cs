using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

/// <summary>
/// 3.5b.1 — HealthPlan aggregate root (ADR-0016). The plan is a
/// configuration object: it groups <see cref="HealthPlanItem"/>s and is
/// scoped to a single species. The unit tests live here because the
/// invariants are pure and do not need a database; the integration tests
/// prove the schema agrees with the model.
/// </summary>
public class HealthPlanTests
{
    private static readonly Guid SpeciesId = Guid.NewGuid();
    private static readonly Guid OtherSpeciesId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidName_IsActiveByDefault()
    {
        var plan = HealthPlan.Create("Cronograma porcino de engorde", SpeciesId);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Cronograma porcino de engorde", plan.Name);
        Assert.Equal(SpeciesId, plan.SpeciesId);
        Assert.True(plan.IsActive);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlan.Create("   ", SpeciesId));
    }

    [Fact]
    public void Create_WithEmptySpeciesId_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlan.Create("Vacunación Bovinos", Guid.Empty));
    }

    [Fact]
    public void Create_WithDuplicateNameSameSpecies_DoesNotThrowAtDomainLevel()
    {
        // The duplicate check is the application layer's job (calls the DB
        // through its handler before invoking Create). The domain itself
        // is pure: it has no database, so two Create() calls in a row both
        // return — the DB's filtered unique index is what stops the second
        // one from persisting. The integration suite covers the
        // end-to-end behaviour; here we only assert the domain lets the
        // application layer make the call.
        var first = HealthPlan.Create("Cronograma", SpeciesId);
        var second = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.SpeciesId, second.SpeciesId);
    }

    [Fact]
    public void Create_WithSameNameDifferentSpecies_IsAllowed()
    {
        // The unique index is (name, species_id); the same name across two
        // species is legitimate because the client may run independent
        // schedules for bovine and porcine herds.
        var a = HealthPlan.Create("Engorde", SpeciesId);
        var b = HealthPlan.Create("Engorde", OtherSpeciesId);

        Assert.Equal("Engorde", a.Name);
        Assert.Equal("Engorde", b.Name);
        Assert.NotEqual(a.SpeciesId, b.SpeciesId);
    }

    [Fact]
    public void AddItem_WithValidData_AppendsAndIsRetrievable()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        var item = plan.AddItem(
            name: "Vacuna A",
            eventType: "Vaccination",
            anchor: PlanAnchor.GroupStart,
            anchorOffsetDays: 21,
            complianceWindowDays: 3);

        Assert.Single(plan.Items);
        Assert.Equal(item, plan.Items[0]);
        Assert.Equal(plan.Id, item.HealthPlanId);
        Assert.Equal("Vaccination", item.EventType);
        Assert.Equal(PlanAnchor.GroupStart, item.Anchor);
        Assert.Equal(21, item.AnchorOffsetDays);
        Assert.Equal(3, item.ComplianceWindowDays);
    }

    [Fact]
    public void AddItem_ToDeactivatedPlan_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);
        plan.Deactivate();

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vaccination", PlanAnchor.Birth, 30, 3));
    }

    [Fact]
    public void AddItem_WithZeroOffsetAndNoRepetition_Throws()
    {
        // The CHECK constraint at the DB level is enforced at the domain too:
        // a zero offset with no repetition would mean "do it at the anchor,
        // and never again" — readable as zero days, but the whole point of
        // recording the offset is to make the timing explicit.
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vaccination", PlanAnchor.Birth, 0, 3));
    }

    [Fact]
    public void AddItem_WithZeroOffsetAndRepetition_Allows()
    {
        // If the operator wants a recurring zero-offset event, the repetitions
        // carry the cadence (e.g. monthly dosing aligned with the anchor).
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        var item = plan.AddItem(
            name: "Desparasitación",
            eventType: "Treatment",
            anchor: PlanAnchor.GroupStart,
            anchorOffsetDays: 0,
            complianceWindowDays: 3,
            repetitions: 30);

        Assert.Equal(0, item.AnchorOffsetDays);
        Assert.Equal(30, item.Repetitions);
    }

    [Fact]
    public void AddItem_WithNegativeOffset_Allows()
    {
        // "14 days before birthing" is core use case: a pre-partum vaccine
        // for the mother lives behind anchorOffsetDays = -14.
        var plan = HealthPlan.Create("Madres", SpeciesId);

        var item = plan.AddItem(
            name: "Pre-parto",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birthing,
            anchorOffsetDays: -14,
            complianceWindowDays: 3);

        Assert.Equal(-14, item.AnchorOffsetDays);
    }

    [Fact]
    public void AddItem_WithInvalidEventType_Throws()
    {
        // event_type is data, not an enum (Art. 8), but the wire-format
        // hygiene is the same: a typo in the panel that produces
        // "vacunacion " or "Vacc!" must be rejected before the row lands.
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vacunación ", PlanAnchor.Birth, 7, 3));
    }

    [Fact]
    public void AddItem_WithEmptyName_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "   ", "Vaccination", PlanAnchor.Birth, 7, 3));
    }

    [Fact]
    public void AddItem_WithNegativeComplianceWindow_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vaccination", PlanAnchor.Birth, 7, -1));
    }

    [Fact]
    public void AddItem_WithZeroComplianceWindow_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vaccination", PlanAnchor.Birth, 7, 0));
    }

    [Fact]
    public void AddItem_WithNonPositiveRepetitions_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);

        Assert.Throws<DomainException>(() => plan.AddItem(
            "Vacuna", "Vaccination", PlanAnchor.Birth, 0, 3, repetitions: 0));
    }

    [Fact]
    public void Deactivate_ThenDeactivateAgain_Throws()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);
        plan.Deactivate();

        Assert.False(plan.IsActive);
        Assert.Throws<DomainException>(() => plan.Deactivate());
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresIt()
    {
        var plan = HealthPlan.Create("Cronograma", SpeciesId);
        plan.Deactivate();

        plan.Activate();

        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Assignment_CreateForAnimal_StoresAnimalIdOnly()
    {
        var assignment = HealthPlanAssignment.CreateForAnimal(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(assignment.HealthPlanId, assignment.HealthPlanId);
        Assert.NotNull(assignment.AnimalId);
        Assert.Null(assignment.GroupId);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public void Assignment_CreateForGroup_StoresGroupIdOnly()
    {
        var assignment = HealthPlanAssignment.CreateForGroup(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(assignment.GroupId);
        Assert.Null(assignment.AnimalId);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public void Assignment_WithBothAnimalAndGroup_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlanAssignment.Create(
            Guid.NewGuid(), animalId: Guid.NewGuid(), groupId: Guid.NewGuid()));
    }

    [Fact]
    public void Assignment_WithNeitherAnimalNorGroup_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlanAssignment.Create(
            Guid.NewGuid(), animalId: null, groupId: null));
    }
}
