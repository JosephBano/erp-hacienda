using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// Employee / User entity for Auth and Permissions (Art. 4 + Art. 8 + GLOSSARY.md + ADR-0007).
/// </summary>
public class User : AuditableEntity
{
    private readonly List<UserRole> _userRoles = [];

    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    private User()
    {
        FullName = null!;
        Email = null!;
        PasswordHash = null!;
    }

    private User(string fullName, string email, string passwordHash)
    {
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public static User Create(string fullName, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre completo no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("El correo electrónico no es válido.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("La contraseña es requerida.");

        return new User(fullName.Trim(), email.Trim().ToLowerInvariant(), passwordHash);
    }

    public void AddRole(Role role, Guid? groupId = null)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (_userRoles.Any(ur => ur.RoleId == role.Id && ur.GroupId == groupId))
            return;

        _userRoles.Add(new UserRole(this, role, groupId));
    }

    public void RemoveRole(Guid roleId, Guid? groupId = null)
    {
        _userRoles.RemoveAll(ur => ur.RoleId == roleId && ur.GroupId == groupId);
    }

    public bool HasRole(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return false;
        var normalizedCode = roleCode.Trim().ToLowerInvariant();
        return _userRoles.Any(ur => ur.Role != null && ur.Role.Code == normalizedCode);
    }

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;
        var normalizedCode = permissionCode.Trim().ToLowerInvariant();
        return _userRoles.Any(ur => ur.Role != null && ur.Role.RolePermissions.Any(rp => rp.Permission != null && rp.Permission.Code == normalizedCode));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public bool VerifyPassword(string password) => PasswordHasher.Verify(password, PasswordHash);
}
