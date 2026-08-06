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

    /// <summary>
    /// Whether the field app should offer this species for milking registration. False by
    /// default (fail-closed): a newly registered species has to be opted in, so a forgotten
    /// species never silently allows milk recording. Pigs, poultry, equines stay false;
    /// bovines and caprines stay true. The admin-web panel is the only place this flag
    /// gets flipped, exactly like gestation days.
    /// </summary>
    public bool IsMilkable { get; private set; }

    private Species(string name, int? gestationDays, bool isMilkable)
    {
        Name = name;
        GestationDays = gestationDays;
        IsMilkable = isMilkable;
    }

    public static Species Create(string name, int? gestationDays = null, bool isMilkable = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la especie no puede estar vacío.");

        if (gestationDays is <= 0)
            throw new DomainException("Los días de gestación deben ser positivos.");

        return new Species(name.Trim(), gestationDays, isMilkable);
    }
}
