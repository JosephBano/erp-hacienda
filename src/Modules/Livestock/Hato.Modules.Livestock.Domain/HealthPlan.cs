using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// A configurable health plan attached to a single species (ADR-0016).
/// The plan is a configuration object — adding a vaccine to the schedule,
/// changing the offset of an existing one, or creating a new plan for a
/// new species is an INSERT from the panel, never a deploy (Art. 8). The
/// name is unique per species among non-deleted plans so the same clinical
/// term ("Engorde") can be reused across bovine and porcine herds without
/// a global contention on the word.
///
/// <see cref="Items"/> is the list of scheduled operations. The shape of
/// each item — anchor, offset, filter — is the whole mechanism that makes
/// one motor satisfy three different feature requests (vaccination by
/// lot, schedule by mother, by-sex procedures): the differentiation lives
/// in data, not in code (ADR-0016 sec."Decisión" 2).
/// </summary>
public class HealthPlan : AuditableEntity
{
    private readonly List<HealthPlanItem> _items = [];

    public string Name { get; private set; }
    public Guid SpeciesId { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<HealthPlanItem> Items => _items.AsReadOnly();

    private HealthPlan() { Name = null!; }

    private HealthPlan(string name, Guid speciesId)
    {
        Name = name;
        SpeciesId = speciesId;
        IsActive = true;
    }

    /// <summary>
    /// The duplicate (name, species_id) check is the application layer's
    /// job: it has the database in scope, and a friendly 400 beats a raw
    /// unique-constraint violation from the database. The domain stays
    /// pure (no I/O, no DbContext) — the same shape MortalityCause and
    /// AdministrationRoute use.
    /// </summary>
    public static HealthPlan Create(string name, Guid speciesId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del plan sanitario no puede estar vacío.");

        if (speciesId == Guid.Empty)
            throw new DomainException("El plan sanitario debe pertenecer a una especie.");

        var trimmed = name.Trim();
        if (trimmed.Length > 120)
            throw new DomainException("El nombre del plan sanitario no puede superar 120 caracteres.");

        return new HealthPlan(trimmed, speciesId);
    }

    /// <summary>
    /// Appends a scheduled operation to this plan. The combination of
    /// <paramref name="anchorOffsetDays"/> and <paramref name="repetitions"/>
    /// carries the cadence: a non-zero offset is an absolute delay from the
    /// anchor (negative values are legitimate — "14 days before birthing");
    /// a repetition is the gap between successive applications once the
    /// schedule starts. The (offset != 0) OR (repetitions IS NULL) check
    /// keeps both shapes honest — picking neither is meaningless.
    /// </summary>
    public HealthPlanItem AddItem(
        string name,
        string eventType,
        PlanAnchor anchor,
        int anchorOffsetDays,
        int complianceWindowDays,
        Guid? inventoryItemId = null,
        Guid? routeId = null,
        decimal? doseQuantity = null,
        Guid? doseUnitId = null,
        int? repetitions = null,
        Guid? appliesToCategoryId = null,
        string? appliesToSex = null)
    {
        if (!IsActive)
            throw new DomainException("No se pueden agregar ítems a un plan inactivo.");

        var item = HealthPlanItem.Create(
            healthPlanId: Id,
            name: name,
            eventType: eventType,
            anchor: anchor,
            anchorOffsetDays: anchorOffsetDays,
            complianceWindowDays: complianceWindowDays,
            inventoryItemId: inventoryItemId,
            routeId: routeId,
            doseQuantity: doseQuantity,
            doseUnitId: doseUnitId,
            repetitions: repetitions,
            appliesToCategoryId: appliesToCategoryId,
            appliesToSex: appliesToSex);

        _items.Add(item);
        return item;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("El plan sanitario ya está inactivo.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("El plan sanitario ya está activo.");

        IsActive = true;
    }
}
