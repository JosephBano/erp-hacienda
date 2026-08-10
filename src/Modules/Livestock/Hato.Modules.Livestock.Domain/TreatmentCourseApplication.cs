using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// One application within a <see cref="TreatmentCourse"/> (task 5/8): a single day's
/// dose, calculated and/or administered.
///
/// <para>
/// <see cref="CalculatedDoseAmount"/> is what the system suggested;
/// <see cref="AdministeredDoseAmount"/> is what the operator says came out of the
/// bottle. Both are optional (task 3 — Art. 10 requires every *present* quantity to
/// carry a unit, not every application to carry a quantity) and **never reconciled**:
/// if they differ, the gap is persisted as-is (task 6 of the sub-plan, Art. 1) —
/// nobody here decides which one was "right".
/// </para>
/// </summary>
public class TreatmentCourseApplication : AuditableEntity
{
    public Guid TreatmentCourseId { get; private set; }
    public int ApplicationNo { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }

    public decimal? CalculatedDoseAmount { get; private set; }
    public string? CalculatedDoseUnit { get; private set; }

    /// <summary>True when <see cref="CalculatedDoseAmount"/> was resolved against a
    /// group's sampled average weight rather than an individual animal's own last
    /// weighing (task 6). The automatic note citing the sample size lives in
    /// <see cref="Notes"/>.</summary>
    public bool IsEstimated { get; private set; }

    public decimal? AdministeredDoseAmount { get; private set; }
    public string? AdministeredDoseUnit { get; private set; }

    /// <summary>Free-text observation (task 4): where what no schema captures lives —
    /// "se aplicó en el cuello porque la pierna estaba lastimada". Never a substitute
    /// for a typed field; required in spirit (not enforced in code, task 3) whenever
    /// <see cref="AdministeredDoseAmount"/> is omitted.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// True when the operator explicitly confirmed a value the local plausibility
    /// check (ADR-0022) flagged as improbable for this animal's species/category
    /// before this application was pushed — the same contract
    /// <c>Weighing</c>/<c>Milking</c> already carry inside their JSON payload
    /// (<c>is_plausibility_confirmed</c>), lifted to a real column here because
    /// <see cref="TreatmentCourseApplication"/> has no free-form payload field to
    /// hide it in. Defaults to <c>false</c>: legacy applications (pre-dating
    /// ADR-0022) and applications the panel creates directly carry no confirmation
    /// because no plausibility check ran against them.
    /// </summary>
    public bool IsPlausibilityConfirmed { get; private set; }

    private TreatmentCourseApplication() { }

    private TreatmentCourseApplication(
        Guid treatmentCourseId,
        int applicationNo,
        DateTimeOffset appliedAt,
        decimal? calculatedDoseAmount,
        string? calculatedDoseUnit,
        bool isEstimated,
        decimal? administeredDoseAmount,
        string? administeredDoseUnit,
        string? notes,
        bool isPlausibilityConfirmed)
    {
        TreatmentCourseId = treatmentCourseId;
        ApplicationNo = applicationNo;
        AppliedAt = appliedAt;
        CalculatedDoseAmount = calculatedDoseAmount;
        CalculatedDoseUnit = calculatedDoseUnit;
        IsEstimated = isEstimated;
        AdministeredDoseAmount = administeredDoseAmount;
        AdministeredDoseUnit = administeredDoseUnit;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsPlausibilityConfirmed = isPlausibilityConfirmed;
    }

    internal static TreatmentCourseApplication Create(
        Guid treatmentCourseId,
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
        if (applicationNo <= 0)
            throw new DomainException("El número de aplicación debe ser mayor a cero.");

        ValidateDoseCoupling(calculatedDoseAmount, calculatedDoseUnit, "dosis calculada");
        ValidateDoseCoupling(administeredDoseAmount, administeredDoseUnit, "dosis administrada");

        return new TreatmentCourseApplication(
            treatmentCourseId, applicationNo, appliedAt,
            calculatedDoseAmount, calculatedDoseUnit?.Trim(), isEstimated,
            administeredDoseAmount, administeredDoseUnit?.Trim(), notes, isPlausibilityConfirmed);
    }

    /// <summary>
    /// Art. 10: every physical quantity carries an explicit unit. A dose is
    /// optional as a whole (task 3), but the moment a value is present it must not
    /// travel without its unit — and a unit without a value is meaningless, so the
    /// pair is all-or-nothing.
    /// </summary>
    private static void ValidateDoseCoupling(decimal? amount, string? unit, string label)
    {
        var hasAmount = amount.HasValue;
        var hasUnit = !string.IsNullOrWhiteSpace(unit);

        if (hasAmount && !hasUnit)
            throw new DomainException(
                $"La {label} tiene un valor pero no una unidad. Art. 10 exige unidad explícita para toda cantidad física.");

        if (!hasAmount && hasUnit)
            throw new DomainException(
                $"La {label} tiene una unidad pero ningún valor. Quite la unidad o indique la cantidad.");

        if (hasAmount && amount <= 0)
            throw new DomainException($"La {label}, si se declara, debe ser mayor a cero.");

        if (hasUnit && unit!.Trim().Length > 20)
            throw new DomainException($"La unidad de la {label} no puede superar 20 caracteres.");
    }
}
