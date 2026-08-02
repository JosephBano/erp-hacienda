using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// Granular Permission entity configured in database (ADR-0007 + Art. 8).
/// </summary>
public class Permission : AuditableEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Module { get; private set; }
    public string Description { get; private set; }

    private Permission()
    {
        Code = null!;
        Name = null!;
        Module = null!;
        Description = null!;
    }

    private Permission(string code, string name, string module, string description)
    {
        Code = code;
        Name = name;
        Module = module;
        Description = description;
    }

    public static Permission Create(string code, string name, string module, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("El código del permiso no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del permiso no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(module))
            throw new DomainException("El módulo del permiso es requerido.");

        return new Permission(code.Trim().ToLowerInvariant(), name.Trim(), module.Trim(), description?.Trim() ?? string.Empty);
    }
}
