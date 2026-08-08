using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

/// <summary>
/// HealthPlan / HealthPlanItem / HealthPlanAssignment domain tests
/// (ADR-0016). The plan is the configurable cronogram the client uses to
/// schedule vaccinations, parasite treatments and procedures. The state
/// machinery — anchor resolution, due-date calculation, fulfillment
/// detection — is the responsibility of the application layer; the domain
/// only enforces structural validity.
/// </summary>
public class HealthPlanTests
{
    private readonly Guid _speciesId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidName_IsActiveByDefault()
    {
        var plan = HealthPlan.Create("Plan de inicio", _speciesId);

        Assert.Equal(_speciesId, plan.SpeciesId);
        Assert.Equal("Plan de inicio", plan.Name);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlan.Create("   ", _speciesId));
    }

    [Fact]
    public void Create_WithEmptySpeciesId_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlan.Create("Plan", Guid.Empty));
    }

    [Fact]
    public void AddItem_StoresItOnThePlan()
    {
        var plan = HealthPlan.Create("Plan", _speciesId);
        var item = plan.AddItem(
            name: "Vacuna 1",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birth,
            anchorOffsetDays: 30,
            complianceWindowDays: 3);

        Assert.Single(plan.Items);
        Assert.Equal(item.Id, plan.Items.First().Id);
    }

    [Fact]
    public void HealthPlanItem_Create_WithZeroOffsetAndNoRepetition_Throws()
    {
        // ADR-0016 sec.1: an item with anchorOffsetDays == 0 AND no repetitions
        // is a degenerate configuration. The client can express "fire on the
        // anchor day" with offset = 1 if they really want it. Rejecting it
        // here keeps the resolver simple.
        Assert.Throws<DomainException>(() => HealthPlanItem.Create(
            healthPlanId: Guid.NewGuid(),
            name: "Vacuna",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birth,
            anchorOffsetDays: 0,
            complianceWindowDays: 3));
    }

    [Fact]
    public void HealthPlanItem_Create_WithNegativeOffset_Allowed()
    {
        // "14 days before farrowing" is a real use case for the gilt.
        var item = HealthPlanItem.Create(
            healthPlanId: Guid.NewGuid(),
            name: "Vacuna pre-parto",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birthing,
            anchorOffsetDays: -14,
            complianceWindowDays: 3);

        Assert.Equal(-14, item.AnchorOffsetDays);
    }

    [Fact]
    public void HealthPlanItem_Create_WithInvalidEventType_Throws()
    {
        // The event_type is a string (Art. 8), but a typo producing a
        // bare-ASCII special character is rejected here so a parser
        // downstream can rely on the on-the-wire format.
        Assert.Throws<DomainException>(() => HealthPlanItem.Create(
            healthPlanId: Guid.NewGuid(),
            name: "Vacuna",
            eventType: "Vaccination!",
            anchor: PlanAnchor.Birth,
            anchorOffsetDays: 30,
            complianceWindowDays: 3));
    }

    [Fact]
    public void HealthPlanItem_Create_WithEmptyName_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlanItem.Create(
            healthPlanId: Guid.NewGuid(),
            name: "  ",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birth,
            anchorOffsetDays: 30,
            complianceWindowDays: 3));
    }

    [Fact]
    public void HealthPlanItem_Create_WithNegativeComplianceWindow_Throws()
    {
        Assert.Throws<DomainException>(() => HealthPlanItem.Create(
            healthPlanId: Guid.NewGuid(),
            name: "Vacuna",
            eventType: "Vaccination",
            anchor: PlanAnchor.Birth,
            anchorOffsetDays: 30,
            complianceWindowDays: -1));
    }

    [Fact]
    public void HealthPlanAssignment_CreateForAnimal_StoresAnimalId()
    {
        var planId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        var assignment = HealthPlanAssignment.CreateForAnimal(planId, animalId);

        Assert.Equal(planId, assignment.HealthPlanId);
        Assert.Equal(animalId, assignment.AnimalId);
        Assert.Null(assignment.GroupId);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public void HealthPlanAssignment_CreateForGroup_StoresGroupId()
    {
        var planId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var assignment = HealthPlanAssignment.CreateForGroup(planId, groupId);

        Assert.Equal(planId, assignment.HealthPlanId);
        Assert.Equal(groupId, assignment.GroupId);
        Assert.Null(assignment.AnimalId);
    }

    [Fact]
    public void HealthPlanAssignment_CreateForAnimal_WithEmptyAnimalId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            HealthPlanAssignment.CreateForAnimal(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void HealthPlanAssignment_CreateForGroup_WithEmptyGroupId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            HealthPlanAssignment.CreateForGroup(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void HealthPlanAssignment_CreateForBoth_Throws()
    {
        // XOR: exactly one of animal/group, not both.
        Assert.Throws<DomainException>(() =>
            HealthPlanAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void HealthPlanAssignment_CreateForNeither_Throws()
    {
        // XOR: exactly one of animal/group, not neither.
        Assert.Throws<DomainException>(() =>
            HealthPlanAssignment.Create(Guid.NewGuid(), null, null));
    }

    [Fact]
    public void HealthPlanAssignment_Deactivate_RestoresIt()
    {
        var assignment = HealthPlanAssignment.CreateForAnimal(Guid.NewGuid(), Guid.NewGuid());

        assignment.Deactivate();

        Assert.False(assignment.IsActive);
    }
}
