using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class UserTests
{
    [Fact]
    public void CreateUser_WithValidData_Succeeds()
    {
        var user = User.Create("Mayordomo Juan", "juan@finca.ec", "hash_secret", UserRole.Registrar);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Mayordomo Juan", user.FullName);
        Assert.Equal("juan@finca.ec", user.Email);
        Assert.Equal(UserRole.Registrar, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void CreateUser_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() => User.Create("Juan", "invalido", "hash", UserRole.Registrar));
    }
}
