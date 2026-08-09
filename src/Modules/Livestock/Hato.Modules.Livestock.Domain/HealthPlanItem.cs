using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// One scheduled operation inside a <see cref="HealthPlan"/> (ADR-0016).
/// The schema is intentionally rich because it is the entire mechanism
/// that makes one motor satisfy the three feature requests the client
/// raised: vaccination by lot, vaccination by mother, and by-sex
/// procedures (castration, preselection). The variety lives in data, not
/// in code — adding a new "kind of thing to schedule" is an INSERT, not
/// a deploy (Art. 8).
///
/// <see cref="EventType"/> is a string on the wire, not an enum: the
/// catalogue of what the operator wants to schedule keeps growing as the
/// client's operation evolves, and a new event type is enough without a
/// schema change. The wire-format hygiene (lower-case, alphanumeric +
/// underscore, no whitespace) is enforced here so a typo in the panel
/// cannot produce a token that looks like a valid one to a parser.
///
/// <see cref="Anchor"/> is a closed enum (ADR-0016 sec."Negativas"):
/// exactly four anchors cover everything the client raised, and adding a
/// fifth is a code change in the resolver.
///
/// <see cref="AnchorOffsetDays"/> can be negative ("14 days before
/// birthing") and is paired with <see cref="Repetitions"/>: if the
/// offset is zero, a repetition cadence is required for the item to make
/// sense (the DB enforces this as a CHECK constraint; the domain enforces
/// the same).
/// </summary>
public class HealthPlanItem : AuditableEntity
{
    private static readonly Regex EventTypePattern = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public Guid HealthPlanId { get; private set; }
    public string Name { get; private set; }

    /// <summary>Wire-format key for the kind of event this item emits (Vaccination, Treatment, …).</summary>
    public string EventType { get; private set; }

    public PlanAnchor Anchor { get; private set; }

    /// <summary>Days from the anchor (negative = before, positive = after). Zero forces a re-cadence row.</summary>
    public int AnchorOffsetDays { get; private set; }

    /// <summary>Symmetric window around the theoretical date: [-w, +w] the application considers "on time".</summary>
    public int ComplianceWindowDays { get; private set; }

    public Guid? InventoryItemId { get; private set; }
    public Guid? RouteId { get; private set; }
    public decimal? DoseQuantity { get; private set; }
    public Guid? DoseUnitId { get; private set; }

    /// <summary>Cadence in days — null means one-off; N means every N days after the anchor.</summary>
    public int? Repetitions { get; private set; }

    public Guid? AppliesToCategoryId { get; private set; }

    /// <summary>
    /// Sex filter: "M", "F", or null for "both". Stored as a string to keep
    /// the column narrow and the field-app's local filter simple.
    /// </summary>
    public string? AppliesToSex { get; private set; }

    public bool IsActive { get; private set; }

    private HealthPlanItem() { Name = null!; EventType = null!; }

    private HealthPlanItem(
        Guid healthPlanId,
        string name,
        string eventType,
        PlanAnchor anchor,
        int anchorOffsetDays,
        int complianceWindowDays,
        Guid? inventoryItemId,
        Guid? routeId,
        decimal? doseQuantity,
        Guid? doseUnitId,
        int? repetitions,
        Guid? appliesToCategoryId,
        string? appliesToSex)
    {
        HealthPlanId = healthPlanId;
        Name = name;
        EventType = eventType;
        Anchor = anchor;
        AnchorOffsetDays = anchorOffsetDays;
        ComplianceWindowDays = complianceWindowDays;
        InventoryItemId = inventoryItemId;
        RouteId = routeId;
        DoseQuantity = doseQuantity;
        DoseUnitId = doseUnitId;
        Repetitions = repetitions;
        AppliesToCategoryId = appliesToCategoryId;
        AppliesToSex = appliesToSex;
        IsActive = true;
    }

    public static HealthPlanItem Create(
        Guid healthPlanId,
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
        if (healthPlanId == Guid.Empty)
            throw new DomainException("El ítem del plan debe pertenecer a un plan sanitario.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del ítem del plan no puede estar vacío.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > 120)
            throw new DomainException("El nombre del ítem del plan no puede superar 120 caracteres.");

        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("El tipo de evento del ítem del plan no puede estar vacío.");

        var trimmedType = eventType.Trim();
        if (!EventTypePattern.IsMatch(trimmedType))
            throw new DomainException(
                "El tipo de evento debe empezar con una letra y contener solo letras, dígitos y guion bajo.");

        if (complianceWindowDays <= 0)
            throw new DomainException("La ventana de cumplimiento debe ser positiva.");

        if (repetitions is <= 0)
            throw new DomainException("La repetición, si se declara, debe ser un entero positivo.");

        if (anchorOffsetDays == 0 && repetitions is null)
            throw new DomainException(
                "El desfase no puede ser cero sin una repetición que defina la cadencia.");

        if (doseQuantity is < 0)
            throw new DomainException("La dosis no puede ser negativa.");

        if (appliesToSex is not null && appliesToSex is not ("M" or "F"))
            throw new DomainException(
                "El filtro de sexo debe ser 'M', 'F' o nulo (ambos).");

        return new HealthPlanItem(
            healthPlanId,
            trimmedName,
            trimmedType,
            anchor,
            anchorOffsetDays,
            complianceWindowDays,
            inventoryItemId,
            routeId,
            doseQuantity,
            doseUnitId,
            repetitions,
            appliesToCategoryId,
            appliesToSex);
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("El ítem del plan ya está inactivo.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("El ítem del plan ya está activo.");

        IsActive = true;
    }
}
