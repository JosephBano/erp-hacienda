namespace Hato.Modules.People.Domain;

/// <summary>
/// N:N relationship between Role and Permission with optional GroupId scope (ADR-0007).
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid? GroupId { get; private set; }

    public Role Role { get; internal set; } = null!;
    public Permission Permission { get; internal set; } = null!;

    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId, Guid? groupId = null)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GroupId = groupId;
    }

    public RolePermission(Role role, Permission permission, Guid? groupId = null)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(permission);
        RoleId = role.Id;
        PermissionId = permission.Id;
        Role = role;
        Permission = permission;
        GroupId = groupId;
    }
}
