using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Configurable catalog of why an animal died (Art. 8): crushing, starvation, weak at
/// birth, diarrhea, hernia, unknown… The farm's own list, not a fixed enum — "unknown"
/// covers the gap while the client refines the rest (docs/planes/fase-3-5/spec.md sec.7-B).
/// Rows are deactivated, never deleted: a cause already referenced by history stays
/// readable (Art. 1) even after the panel retires it from new use.
/// </summary>
public class MortalityCause : AuditableEntity
{
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    private MortalityCause() { Name = null!; }

    private MortalityCause(string name)
    {
        Name = name;
        IsActive = true;
    }

    public static MortalityCause Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la causa de mortalidad no puede estar vacío.");

        return new MortalityCause(name.Trim());
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("La causa de mortalidad ya está inactiva.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("La causa de mortalidad ya está activa.");

        IsActive = true;
    }
}
