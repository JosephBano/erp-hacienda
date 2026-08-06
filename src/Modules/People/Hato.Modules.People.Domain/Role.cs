using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// Role entity configured in database (ADR-0007 + Art. 8).
/// </summary>
public class Role : AuditableEntity
{
    private readonly List<RolePermission> _permissions = [];

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _permissions;

    private Role()
    {
        Code = null!;
        Name = null!;
        Description = null!;
    }

    private Role(string code, string name, string description, bool isSystem)
    {
        Code = code;
        Name = name;
        Description = description;
        IsSystem = isSystem;
    }

    public static Role Create(string code, string name, string description, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("El código del rol no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del rol no puede estar vacío.");

        return new Role(code.Trim().ToLowerInvariant(), name.Trim(), description?.Trim() ?? string.Empty, isSystem);
    }

    public void UpdateDetails(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del rol no puede estar vacío.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public void AddPermission(Permission permission, Guid? groupId = null)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (_permissions.Any(rp => rp.PermissionId == permission.Id && rp.GroupId == groupId))
            return;

        _permissions.Add(new RolePermission(this, permission, groupId));
    }

    public void RemovePermission(Guid permissionId, Guid? groupId = null)
    {
        _permissions.RemoveAll(rp => rp.PermissionId == permissionId && rp.GroupId == groupId);
    }
}
