using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class UserTests
{
    [Fact]
    public void CreateUser_WithValidData_Succeeds()
    {
        var user = User.Create("Mayordomo Juan", "juan@finca.ec", "hash_secret");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Mayordomo Juan", user.FullName);
        Assert.Equal("juan@finca.ec", user.Email);
        Assert.True(user.IsActive);
        Assert.Empty(user.UserRoles);
    }

    [Fact]
    public void CreateUser_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() => User.Create("Juan", "invalido", "hash"));
    }

    [Fact]
    public void AddRoleAndPermission_ResolvesCorrectly()
    {
        var user = User.Create("Mayordomo Juan", "juan@finca.ec", "hash_secret");
        var role = Role.Create("registrar", "Registrador de Campo", "Registra ordeño y salud");
        var permMilking = Permission.Create("production.milking.record", "Registrar Ordeño", "Production", "Permite registrar producción de leche");

        role.AddPermission(permMilking);
        user.AddRole(role);

        Assert.Single(user.UserRoles);
        Assert.True(user.HasRole("registrar"));
        Assert.True(user.HasPermission("production.milking.record"));
        Assert.False(user.HasPermission("people.users.manage"));
    }

    [Fact]
    public void Role_AddDuplicatePermission_IsIdempotent()
    {
        var role = Role.Create("registrar", "Registrador", "Desc");
        var perm = Permission.Create("livestock.animals.read", "Ver animales", "Livestock", "Desc");

        role.AddPermission(perm);
        role.AddPermission(perm);

        Assert.Single(role.RolePermissions);
    }

    [Fact]
    public void User_RemoveRole_RemovesCorrectly()
    {
        var user = User.Create("Mayordomo Juan", "juan@finca.ec", "hash_secret");
        var role = Role.Create("registrar", "Registrador", "Desc");

        user.AddRole(role);
        Assert.Single(user.UserRoles);

        user.RemoveRole(role.Id);
        Assert.Empty(user.UserRoles);
    }
}
