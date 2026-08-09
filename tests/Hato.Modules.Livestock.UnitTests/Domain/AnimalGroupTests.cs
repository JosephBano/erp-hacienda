using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class AnimalGroupTests
{
    [Fact]
    public void Create_WithValidName_Succeeds()
    {
        var group = AnimalGroup.Create("Vacas Lecheras", "Lote de ordeño principal");

        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Equal("Vacas Lecheras", group.Name);
        Assert.Equal("Lote de ordeño principal", group.Description);
        Assert.True(group.IsActive);
        Assert.Empty(group.Memberships);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<DomainException>(() => AnimalGroup.Create("   "));
    }

    [Fact]
    public void AddMember_ActiveGroup_CreatesActiveMembership()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        var animalId = Guid.NewGuid();
        var joinedAt = new DateOnly(2026, 1, 1);

        var membership = group.AddMember(animalId, joinedAt);

        Assert.Single(group.Memberships);
        Assert.True(membership.IsActive);
        Assert.Equal(animalId, membership.AnimalId);
        Assert.Equal(joinedAt, membership.JoinedAt);
    }

    [Fact]
    public void AddMember_DuplicateActiveMember_Throws()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        var animalId = Guid.NewGuid();
        var joinedAt = new DateOnly(2026, 1, 1);

        group.AddMember(animalId, joinedAt);

        Assert.Throws<DomainException>(() => group.AddMember(animalId, joinedAt));
    }

    [Fact]
    public void RemoveMember_ActiveMember_ClosesMembership()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        var animalId = Guid.NewGuid();
        var joinedAt = new DateOnly(2026, 1, 1);
        var leftAt = new DateOnly(2026, 6, 1);

        group.AddMember(animalId, joinedAt);
        group.RemoveMember(animalId, leftAt);

        var membership = group.Memberships.Single();
        Assert.False(membership.IsActive);
        Assert.Equal(leftAt, membership.LeftAt);
    }

    [Fact]
    public void RemoveMember_LeftAtBeforeJoinedAt_Throws()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        var animalId = Guid.NewGuid();
        var joinedAt = new DateOnly(2026, 6, 1);
        var leftAt = new DateOnly(2026, 1, 1);

        group.AddMember(animalId, joinedAt);

        Assert.Throws<DomainException>(() => group.RemoveMember(animalId, leftAt));
    }

    [Fact]
    public void AddMember_ToInactiveGroup_Throws()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        group.Deactivate();

        Assert.Throws<DomainException>(() => group.AddMember(Guid.NewGuid(), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_WithoutTrackingMode_DefaultsToIndividual()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");

        Assert.Equal(TrackingMode.Individual, group.TrackingMode);
    }

    [Fact]
    public void Create_WithHeadcountMode_Persists()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);

        Assert.Equal(TrackingMode.Headcount, group.TrackingMode);
    }

    [Fact]
    public void CloseAllActiveMemberships_ClosesEveryActiveMember_AndReturnsTheirIds()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);
        var joinedAt = new DateOnly(2026, 1, 1);
        var closedAt = new DateOnly(2026, 6, 1);
        var animalIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in animalIds)
        {
            group.AddMember(id, joinedAt);
        }

        var returned = group.CloseAllActiveMemberships(closedAt);

        Assert.Equal(animalIds.ToHashSet(), returned.ToHashSet());
        Assert.All(group.Memberships, m => Assert.False(m.IsActive));
        Assert.All(group.Memberships, m => Assert.Equal(closedAt, m.LeftAt));
    }

    [Fact]
    public void CloseAllActiveMemberships_LeavesAlreadyClosedMembersUntouched()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);
        var animalA = Guid.NewGuid();
        var animalB = Guid.NewGuid();
        var joinedAt = new DateOnly(2026, 1, 1);

        group.AddMember(animalA, joinedAt);
        group.AddMember(animalB, joinedAt);
        group.RemoveMember(animalA, new DateOnly(2026, 3, 1));

        var returned = group.CloseAllActiveMemberships(new DateOnly(2026, 6, 1));

        Assert.Equal([animalB], returned);
        Assert.Equal(new DateOnly(2026, 3, 1), group.Memberships.Single(m => m.AnimalId == animalA).LeftAt);
    }

    [Fact]
    public void CloseAllActiveMemberships_OnGroupWithNothingActive_ReturnsEmpty()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);

        var returned = group.CloseAllActiveMemberships(new DateOnly(2026, 6, 1));

        Assert.Empty(returned);
    }

    // === ADR-0025: Activate, ChangeTrackingMode, idempotencia de Deactivate ===

    [Fact]
    public void Activate_OnInactiveGroup_SetsIsActiveTrue()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        group.Deactivate();
        Assert.False(group.IsActive);

        group.Activate();

        Assert.True(group.IsActive);
    }

    [Fact]
    public void Activate_OnActiveGroup_IsNoOp()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        Assert.True(group.IsActive);

        group.Activate();

        Assert.True(group.IsActive);
    }

    [Fact]
    public void Deactivate_Twice_IsIdempotent()
    {
        var group = AnimalGroup.Create("Vacas Lecheras");
        group.Deactivate();
        group.Deactivate();

        Assert.False(group.IsActive);
    }

    [Fact]
    public void ChangeTrackingMode_SameMode_IsNoOp()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);

        group.ChangeTrackingMode(TrackingMode.Headcount);

        Assert.Equal(TrackingMode.Headcount, group.TrackingMode);
    }

    [Fact]
    public void ChangeTrackingMode_OnInactive_Throws()
    {
        var group = AnimalGroup.Create("Engorde 1", trackingMode: TrackingMode.Headcount);
        group.Deactivate();

        var ex = Assert.Throws<DomainException>(() =>
            group.ChangeTrackingMode(TrackingMode.Individual));

        Assert.Contains("inactivo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeTrackingMode_OnActive_UpdatesMode()
    {
        var group = AnimalGroup.Create("Vacas Lecheras", trackingMode: TrackingMode.Individual);

        group.ChangeTrackingMode(TrackingMode.Headcount);

        Assert.Equal(TrackingMode.Headcount, group.TrackingMode);
        Assert.True(group.IsActive);
    }
}
