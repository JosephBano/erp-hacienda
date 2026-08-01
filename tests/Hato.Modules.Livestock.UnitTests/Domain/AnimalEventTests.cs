using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class AnimalEventTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var animalId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var payload = "{\"weightKg\": 450.5}";

        var evt = AnimalEvent.Create(animalId, EventType.Weighing, occurredAt, "ordeñador", payload, 0);

        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal(animalId, evt.AnimalId);
        Assert.Equal(EventType.Weighing, evt.EventType);
        Assert.Equal("ordeñador", evt.RecordedBy);
        Assert.Equal(payload, evt.PayloadJson);
    }

    [Fact]
    public void Create_WithoutAnimalId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(Guid.Empty, EventType.Weighing, DateTimeOffset.UtcNow, "admin", "{}"));
    }

    [Fact]
    public void Create_WithNegativeCost_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(Guid.NewGuid(), EventType.Treatment, DateTimeOffset.UtcNow, "vet", "{}", cost: -10));
    }

    [Fact]
    public void WithdrawalPeriod_ActiveWindow_EvaluatesCorrectly()
    {
        var period = new WithdrawalPeriod(
            Guid.NewGuid(), Guid.NewGuid(), WithdrawalTarget.Milk, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 7));

        Assert.True(period.IsActiveOn(new DateOnly(2026, 8, 1)));
        Assert.True(period.IsActiveOn(new DateOnly(2026, 8, 5)));
        Assert.True(period.IsActiveOn(new DateOnly(2026, 8, 7)));
        Assert.False(period.IsActiveOn(new DateOnly(2026, 7, 31)));
        Assert.False(period.IsActiveOn(new DateOnly(2026, 8, 8)));
    }

    [Fact]
    public void WithdrawalPeriod_EndsAtBeforeStartsAt_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new WithdrawalPeriod(Guid.NewGuid(), Guid.NewGuid(), WithdrawalTarget.Milk, new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1)));
    }
}
