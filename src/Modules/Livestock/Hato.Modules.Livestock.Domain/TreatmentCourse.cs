using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// A treatment of several days modelled as **one** series with its applications
/// (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-B task 5), not as N disconnected
/// <see cref="AnimalEvent"/> rows. The withdrawal period is computed from
/// <see cref="Applications"/>'s last <c>AppliedAt</c>, never per loose application.
///
/// <para>
/// Subject is exactly one of <see cref="AnimalId"/> / <see cref="GroupId"/> — the
/// same XOR <see cref="AnimalEvent"/> already enforces (ADR-0015 sec.2): "treated
/// this animal for 3 days" and "treated this lot for 3 days" are the same course
/// shape with a different subject, not two entities.
/// </para>
/// </summary>
public class TreatmentCourse : AuditableEntity
{
    // Same wire-format keys as AnimalEvent.KnownTreatmentReasons (3.5a.2-A catalogue).
    // Duplicated rather than shared because the two entities have no common base
    // that owns "treatment reason" — a third table-backed catalogue is future work
    // if a fourth reason ever needs to be added without touching two classes.
    private static readonly HashSet<string> KnownTreatmentReasons = new(StringComparer.Ordinal)
    {
        "scheduled",
        "curative",
        "preventive",
    };

    private readonly List<TreatmentCourseApplication> _applications = [];

    public Guid? AnimalId { get; private set; }
    public Guid? GroupId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public Guid RouteId { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>Soft FK to an inventory product/item. Nullable and unenforced at the
    /// database level — the inventory catalog does not share a schema with
    /// livestock (Art. 6); the strict cross-module contract is Phase 4 work, same
    /// posture as <see cref="AnimalEvent.BatchId"/>.</summary>
    public Guid? ProductId { get; private set; }

    public Guid DoseKindId { get; private set; }
    public decimal DoseFactorAmount { get; private set; }
    public string DoseFactorUnit { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>True when this course was synthesised from a legacy
    /// <see cref="AnimalEvent"/> with a free-text <c>dose</c> payload (task 9): the
    /// backward-compat migration path, not an operator-authored course.</summary>
    public bool IsSynthetic { get; private set; }

    public IReadOnlyCollection<TreatmentCourseApplication> Applications => _applications.AsReadOnly();

    private TreatmentCourse() { DoseFactorUnit = null!; }

    private TreatmentCourse(
        Guid? animalId,
        Guid? groupId,
        DateTimeOffset startsAt,
        Guid routeId,
        string? reason,
        Guid? productId,
        Guid doseKindId,
        decimal doseFactorAmount,
        string doseFactorUnit,
        string? notes,
        bool isSynthetic)
    {
        AnimalId = animalId;
        GroupId = groupId;
        StartsAt = startsAt;
        EndsAt = startsAt;
        RouteId = routeId;
        Reason = reason;
        ProductId = productId;
        DoseKindId = doseKindId;
        DoseFactorAmount = doseFactorAmount;
        DoseFactorUnit = doseFactorUnit;
        Notes = notes?.Trim();
        IsSynthetic = isSynthetic;
    }

    public static TreatmentCourse CreateForAnimal(
        Guid animalId,
        DateTimeOffset startsAt,
        Guid routeId,
        string? reason,
        Guid? productId,
        Guid doseKindId,
        decimal doseFactorAmount,
        string doseFactorUnit,
        string? notes,
        bool isSynthetic = false)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("Una serie de tratamiento individual debe estar asociada a un animal.");

        Validate(routeId, doseKindId, doseFactorAmount, doseFactorUnit);

        return new TreatmentCourse(
            animalId, null, startsAt, routeId, NormaliseReason(reason), productId,
            doseKindId, doseFactorAmount, doseFactorUnit.Trim(), notes, isSynthetic);
    }

    public static TreatmentCourse CreateForGroup(
        Guid groupId,
        DateTimeOffset startsAt,
        Guid routeId,
        string? reason,
        Guid? productId,
        Guid doseKindId,
        decimal doseFactorAmount,
        string doseFactorUnit,
        string? notes,
        bool isSynthetic = false)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("Una serie de tratamiento grupal debe estar asociada a un lote.");

        Validate(routeId, doseKindId, doseFactorAmount, doseFactorUnit);

        return new TreatmentCourse(
            null, groupId, startsAt, routeId, NormaliseReason(reason), productId,
            doseKindId, doseFactorAmount, doseFactorUnit.Trim(), notes, isSynthetic);
    }

    /// <summary>
    /// Appends one application to the series (task 5/8). <paramref name="applicationNo"/>
    /// is 1-based and must not repeat within the course — the caller (the handler,
    /// which knows the full series) assigns it, the aggregate only guards the
    /// invariant. <see cref="EndsAt"/> tracks the latest <c>appliedAt</c> seen so far,
    /// which is exactly what the withdrawal period is computed from (task 5/9).
    /// </summary>
    public TreatmentCourseApplication AddApplication(
        int applicationNo,
        DateTimeOffset appliedAt,
        decimal? calculatedDoseAmount,
        string? calculatedDoseUnit,
        bool isEstimated,
        decimal? administeredDoseAmount,
        string? administeredDoseUnit,
        string? notes,
        bool isPlausibilityConfirmed = false)
    {
        if (_applications.Any(a => a.ApplicationNo == applicationNo))
            throw new DomainException(
                $"La serie ya tiene una aplicación número {applicationNo}.");

        var application = TreatmentCourseApplication.Create(
            Id, applicationNo, appliedAt, calculatedDoseAmount, calculatedDoseUnit,
            isEstimated, administeredDoseAmount, administeredDoseUnit, notes, isPlausibilityConfirmed);

        _applications.Add(application);

        if (appliedAt > (EndsAt ?? StartsAt))
            EndsAt = appliedAt;

        return application;
    }

    private static void Validate(Guid routeId, Guid doseKindId, decimal doseFactorAmount, string doseFactorUnit)
    {
        if (routeId == Guid.Empty)
            throw new DomainException("La vía de administración es obligatoria.");

        if (doseKindId == Guid.Empty)
            throw new DomainException("La forma de dosis es obligatoria.");

        if (doseFactorAmount <= 0)
            throw new DomainException("El factor de dosis debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(doseFactorUnit))
            throw new DomainException("El factor de dosis requiere una unidad explícita (Art. 10).");

        if (doseFactorUnit.Trim().Length > 20)
            throw new DomainException("La unidad del factor de dosis no puede superar 20 caracteres.");
    }

    private static string? NormaliseReason(string? raw)
    {
        if (raw is null) return null;

        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return null;

        var lowered = trimmed.ToLowerInvariant();

        if (!Regex.IsMatch(lowered, "^[a-z0-9_]+$"))
            throw new DomainException(
                "El motivo del tratamiento debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        if (!KnownTreatmentReasons.Contains(lowered))
            throw new DomainException(
                $"El motivo '{lowered}' no existe en el catálogo. Use uno de: scheduled, curative, preventive.");

        return lowered;
    }
}
