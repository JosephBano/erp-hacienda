using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// Employee / User entity for Auth and Permissions (Art. 4 + GLOSSARY.md).
/// </summary>
public class User : AuditableEntity
{
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    private User()
    {
        FullName = null!;
        Email = null!;
        PasswordHash = null!;
    }

    private User(string fullName, string email, string passwordHash, UserRole role)
    {
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    public static User Create(string fullName, string email, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre completo no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("El correo electrónico no es válido.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("La contraseña es requerida.");

        return new User(fullName.Trim(), email.Trim().ToLowerInvariant(), passwordHash, role);
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
