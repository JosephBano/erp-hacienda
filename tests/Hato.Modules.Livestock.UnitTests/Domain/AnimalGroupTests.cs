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
}
