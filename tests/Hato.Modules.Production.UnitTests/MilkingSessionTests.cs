using Hato.Modules.Production.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Production.UnitTests;

public class MilkingSessionTests
{
    [Fact]
    public void CreateSession_WithValidData_Succeeds()
    {
        var session = MilkingSession.Create(new DateOnly(2026, 8, 1), MilkingShift.Morning, " ordeñador 1 ");

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("ordeñador 1", session.RecordedBy);
        Assert.Equal(0, session.TotalLiters);
        Assert.Empty(session.Yields);
    }

    [Fact]
    public void RecordAnimalYield_RecalculatesTotalLiters()
    {
        var session = MilkingSession.Create(new DateOnly(2026, 8, 1), MilkingShift.Morning, "ordeñador");
        var animal1 = Guid.NewGuid();
        var animal2 = Guid.NewGuid();

        session.RecordAnimalYield(animal1, 14.5m);
        session.RecordAnimalYield(animal2, 16.0m);

        Assert.Equal(2, session.Yields.Count);
        Assert.Equal(30.5m, session.TotalLiters);
    }

    [Fact]
    public void RecordAnimalYield_NegativeOrZeroLiters_Throws()
    {
        var session = MilkingSession.Create(new DateOnly(2026, 8, 1), MilkingShift.Morning, "ordeñador");

        Assert.Throws<DomainException>(() => session.RecordAnimalYield(Guid.NewGuid(), 0));
        Assert.Throws<DomainException>(() => session.RecordAnimalYield(Guid.NewGuid(), -5));
    }

    [Fact]
    public void Lactation_StartAndClose_UpdatesState()
    {
        var animalId = Guid.NewGuid();
        var lactation = Lactation.Start(animalId, 1, new DateOnly(2026, 1, 1));

        Assert.True(lactation.IsActive);
        Assert.Equal(1, lactation.LactationNumber);

        lactation.Close(new DateOnly(2026, 10, 1));

        Assert.False(lactation.IsActive);
        Assert.Equal(new DateOnly(2026, 10, 1), lactation.EndDate);
    }
}
