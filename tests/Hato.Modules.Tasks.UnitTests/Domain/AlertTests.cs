using Hato.Modules.Tasks.Domain;
using Hato.Modules.Tasks.Domain.Enums;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Tasks.UnitTests.Domain;

public class AlertTests
{
    [Fact]
    public void Create_ValidData_CreatesAlert()
    {
        var alert = Alert.Create("UPCOMING_BIRTH", "Parto Próximo", "La vaca Pinta tiene fecha de parto estimada en 5 días", AlertSeverity.Warning);

        Assert.NotNull(alert);
        Assert.Equal("UPCOMING_BIRTH", alert.Code);
        Assert.False(alert.IsDismissed);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void Dismiss_MarksAsDismissed()
    {
        var alert = Alert.Create("UPCOMING_BIRTH", "Parto Próximo", "La vaca Pinta tiene fecha de parto estimada en 5 días", AlertSeverity.Warning);
        var userId = Guid.NewGuid();

        alert.Dismiss(userId);

        Assert.True(alert.IsDismissed);
        Assert.NotNull(alert.DismissedAt);
        Assert.Equal(userId, alert.DismissedBy);
    }

    [Fact]
    public void Create_EmptyTitle_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            Alert.Create("CODE", "", "Message", AlertSeverity.Info));
    }
}
