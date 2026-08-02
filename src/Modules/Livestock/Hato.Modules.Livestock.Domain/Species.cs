using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// A livestock species (bovine, porcine, equine…). Configuration data, not code (Art. 8):
/// adding a new species is an INSERT, never a deploy.
/// </summary>
public class Species : AuditableEntity
{
    public string Name { get; private set; }

    /// <summary>Default gestation length, used to project expected birth dates (Phase 2).</summary>
    public int? GestationDays { get; private set; }

    private Species(string name, int? gestationDays)
    {
        Name = name;
        GestationDays = gestationDays;
    }

    public static Species Create(string name, int? gestationDays = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la especie no puede estar vacío.");

        if (gestationDays is <= 0)
            throw new DomainException("Los días de gestación deben ser positivos.");

        return new Species(name.Trim(), gestationDays);
    }
}
