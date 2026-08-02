using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class AuditLogTests
{
    [Fact]
    public void Create_ValidParameters_CreatesAuditLogInstance()
    {
        var userId = Guid.NewGuid();
        var log = AuditLog.Create(
            userId,
            "admin@hacienda.ec",
            "Admin Finca",
            "Create",
            "Livestock",
            "Animal",
            Guid.NewGuid().ToString(),
            "{\"tag\": \"101\"}");

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(userId, log.UserId);
        Assert.Equal("admin@hacienda.ec", log.UserEmail);
        Assert.Equal("Admin Finca", log.UserFullName);
        Assert.Equal("Create", log.Action);
        Assert.Equal("Livestock", log.Module);
        Assert.Equal("Animal", log.EntityName);
        Assert.NotNull(log.DetailsJson);
    }

    [Theory]
    [InlineData("", "Animal")]
    [InlineData("Create", "")]
    public void Create_InvalidParameters_ThrowsArgumentException(string action, string entityName)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.Create(Guid.NewGuid(), "test@hacienda.ec", "User", action, "Module", entityName, "123"));
    }
}
