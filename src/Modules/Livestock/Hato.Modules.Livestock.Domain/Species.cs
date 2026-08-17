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

    /// <summary>
    /// Days of lactation used to compute the weaning date of a nursing cohort
    /// (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4). Null until an operator sets it; the
    /// application layer rejects cohort weaning for a species that has not declared
    /// one yet, instead of guessing 24 the way the macro plan used to do. The
    /// values are deliberately per species and not constants in code: a 24-day
    /// weaning is a porcine reality, not a universal one.
    /// </summary>
    public int? DaysOfLactation { get; private set; }

    /// <summary>
    /// Number of days during which a subsequent birthing joins the open cohort instead
    /// of opening a new one (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.4). The client's operation
    /// is "lunes camada de la cerda 1, martes camada de la cerda 2, miércoles camada de la
    /// cerda 3; todos se destetan juntos" — a window of 5 captures that without needing
    /// a new cohort every two days. Null until configured; cohort auto-assignment then
    /// degrades to "always open a new cohort", which is the safe default.
    /// </summary>
    public int? CohortWindowDays { get; private set; }

    private Species(string name, int? gestationDays, bool isMilkable, int? daysOfLactation, int? cohortWindowDays)
    {
        Name = name;
        GestationDays = gestationDays;
        IsMilkable = isMilkable;
        DaysOfLactation = daysOfLactation;
        CohortWindowDays = cohortWindowDays;
    }

    public static Species Create(
        string name,
        int? gestationDays = null,
        bool isMilkable = false,
        int? daysOfLactation = null,
        int? cohortWindowDays = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la especie no puede estar vacío.");

        if (gestationDays is <= 0)
            throw new DomainException("Los días de gestación deben ser positivos.");

        if (daysOfLactation is <= 0)
            throw new DomainException("Los días de lactancia deben ser positivos.");

        if (cohortWindowDays is <= 0)
            throw new DomainException("La ventana de cohorte debe ser positiva.");

        return new Species(name.Trim(), gestationDays, isMilkable, daysOfLactation, cohortWindowDays);
    }

    /// <summary>
    /// Updates the lactation parameters. Called by the admin panel when the operator
    /// adjusts the species configuration; the field app reads the result via the sync
    /// pull so the change lands without a deploy (Art. 8).
    /// </summary>
    public void UpdateLactation(int? daysOfLactation, int? cohortWindowDays)
    {
        if (daysOfLactation is <= 0)
            throw new DomainException("Los días de lactancia deben ser positivos.");
        if (cohortWindowDays is <= 0)
            throw new DomainException("La ventana de cohorte debe ser positiva.");

        DaysOfLactation = daysOfLactation;
        CohortWindowDays = cohortWindowDays;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
