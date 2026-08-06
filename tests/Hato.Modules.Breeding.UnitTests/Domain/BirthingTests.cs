using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Breeding.UnitTests.Domain;

public class BirthingTests
{
    [Fact]
    public void Create_SingleCalf_Succeeds()
    {
        var damId = Guid.NewGuid();
        var birthDate = new DateOnly(2026, 8, 1);

        var birthing = Birthing.Create(damId, birthDate, BirthingDifficulty.Normal, bornAlive: 1);

        Assert.NotNull(birthing);
        Assert.Equal(1, birthing.TotalBorn);
        Assert.Equal(1, birthing.BornAlive);
        Assert.Single(birthing.DomainEvents);
    }

    [Fact]
    public void Create_PorcineLitter_CalculatesTotalBorn()
    {
        var damId = Guid.NewGuid();
        var birthDate = new DateOnly(2026, 8, 1);

        var birthing = Birthing.Create(
            damId,
            birthDate,
            BirthingDifficulty.Normal,
            bornAlive: 10,
            bornDead: 2,
            mummified: 1,
            litterWeight: 14.5m);

        Assert.Equal(13, birthing.TotalBorn);
        Assert.Equal(10, birthing.BornAlive);
        Assert.Equal(2, birthing.BornDead);
        Assert.Equal(1, birthing.Mummified);
        Assert.Equal(14.5m, birthing.LitterWeight);
    }

    [Fact]
    public void Create_ZeroBorn_ThrowsDomainException()
    {
        var damId = Guid.NewGuid();
        var birthDate = new DateOnly(2026, 8, 1);

        var ex = Assert.Throws<DomainException>(() =>
            Birthing.Create(damId, birthDate, BirthingDifficulty.Normal, bornAlive: 0, bornDead: 0, mummified: 0));

        Assert.Contains("Total born count must be greater than zero", ex.Message);
    }

    [Fact]
    public void Create_WithCohortId_StoresCohortMembership()
    {
        var damId = Guid.NewGuid();
        var cohortId = Guid.NewGuid();

        var birthing = Birthing.Create(
            damId,
            new DateOnly(2026, 8, 1),
            BirthingDifficulty.Normal,
            bornAlive: 11,
            nursingCohortId: cohortId);

        Assert.Equal(cohortId, birthing.NursingCohortId);
    }

    [Fact]
    public void Create_WithoutCohortId_LeavesCohortNull()
    {
        var birthing = Birthing.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            BirthingDifficulty.Normal,
            bornAlive: 1);

        Assert.Null(birthing.NursingCohortId);
    }
}
