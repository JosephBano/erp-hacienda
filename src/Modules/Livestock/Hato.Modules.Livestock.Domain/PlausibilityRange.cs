using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Configurable plausibility range for a physical magnitude (Art. 8): a row per
/// (species, category, magnitude) where magnitude is a free string like
/// "weight_kg" or "milk_liters". The four bounds classify a recorded value into
/// one of three verdicts (ADR-0022 sec.2):
///
///   [plausible_min, plausible_max]  → pass
///   (absolute_min, plausible_min) ∪ (plausible_max, absolute_max)  → confirm
///   ≤ absolute_min  ∪  ≥ absolute_max  → block
///
/// Any of the four bounds may be null (= "this side is not configured"). When
/// none of the four bounds is present, the evaluator must fall back to "pass"
/// (ADR-0022 sec.3 fail-open). The unique constraint
/// (species_id, category_id, magnitude) is enforced by the database — the
/// entity only enforces structural validity.
/// </summary>
public class PlausibilityRange : AuditableEntity
{
    /// <summary>
    /// Magnitude key validation: lower-snake-case, ASCII letters/digits/underscore,
    /// 1–50 characters. Same regex family as <see cref="AdministrationRoute.Key"/>.
    /// </summary>
    private static readonly Regex MagnitudePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    public Guid SpeciesId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Magnitude { get; private set; }
    public decimal? PlausibleMin { get; private set; }
    public decimal? PlausibleMax { get; private set; }
    public decimal? AbsoluteMin { get; private set; }
    public decimal? AbsoluteMax { get; private set; }
    public bool IsActive { get; private set; }

    private PlausibilityRange() { Magnitude = null!; }

    private PlausibilityRange(
        Guid speciesId,
        Guid? categoryId,
        string magnitude,
        decimal? plausibleMin,
        decimal? plausibleMax,
        decimal? absoluteMin,
        decimal? absoluteMax)
    {
        SpeciesId = speciesId;
        CategoryId = categoryId;
        Magnitude = magnitude;
        PlausibleMin = plausibleMin;
        PlausibleMax = plausibleMax;
        AbsoluteMin = absoluteMin;
        AbsoluteMax = absoluteMax;
        IsActive = true;
    }

    public static PlausibilityRange Create(
        Guid speciesId,
        Guid? categoryId,
        string magnitude,
        decimal? plausibleMin,
        decimal? plausibleMax,
        decimal? absoluteMin,
        decimal? absoluteMax)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("La especie del rango de plausibilidad no puede estar vacía.");

        ValidateMagnitude(magnitude);
        ValidateBounds(plausibleMin, plausibleMax, absoluteMin, absoluteMax);

        return new PlausibilityRange(
            speciesId, categoryId, magnitude.Trim(),
            plausibleMin, plausibleMax, absoluteMin, absoluteMax);
    }

    public void UpdateBounds(
        decimal? plausibleMin,
        decimal? plausibleMax,
        decimal? absoluteMin,
        decimal? absoluteMax)
    {
        ValidateBounds(plausibleMin, plausibleMax, absoluteMin, absoluteMax);

        PlausibleMin = plausibleMin;
        PlausibleMax = plausibleMax;
        AbsoluteMin = absoluteMin;
        AbsoluteMax = absoluteMax;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("El rango de plausibilidad ya está inactivo.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("El rango de plausibilidad ya está activo.");

        IsActive = true;
    }

    private static void ValidateMagnitude(string magnitude)
    {
        if (string.IsNullOrWhiteSpace(magnitude))
            throw new DomainException("La magnitud del rango de plausibilidad no puede estar vacía.");

        var trimmed = magnitude.Trim();
        if (trimmed.Length > 50)
            throw new DomainException(
                $"La magnitud del rango de plausibilidad no puede tener más de 50 caracteres (recibido: {trimmed.Length}).");

        if (!MagnitudePattern.IsMatch(trimmed))
            throw new DomainException(
                $"La magnitud '{trimmed}' debe estar en minúsculas, sin espacios, con letras ASCII, dígitos y guión bajo.");
    }

    /// <summary>
    /// Bounds coherence: when all four are present, the order is
    /// absolute_min ≤ plausible_min ≤ plausible_max ≤ absolute_max.
    /// When only some are present, the relevant subset is checked.
    /// Empty (all null) is allowed — the evaluator handles it as fail-open.
    /// </summary>
    private static void ValidateBounds(
        decimal? plausibleMin,
        decimal? plausibleMax,
        decimal? absoluteMin,
        decimal? absoluteMax)
    {
        if (plausibleMin is { } pMin && plausibleMax is { } pMax && pMin > pMax)
            throw new DomainException(
                $"El mínimo plausible ({pMin}) no puede ser mayor que el máximo plausible ({pMax}).");

        if (absoluteMin is { } aMin && absoluteMax is { } aMax && aMin > aMax)
            throw new DomainException(
                $"El mínimo absoluto ({aMin}) no puede ser mayor que el máximo absoluto ({aMax}).");

        if (absoluteMin is { } aMin2 && plausibleMin is { } pMin2 && pMin2 < aMin2)
            throw new DomainException(
                $"El mínimo plausible ({pMin2}) no puede ser menor que el mínimo absoluto ({aMin2}).");

        if (plausibleMax is { } pMax2 && absoluteMax is { } aMax2 && pMax2 > aMax2)
            throw new DomainException(
                $"El máximo plausible ({pMax2}) no puede ser mayor que el máximo absoluto ({aMax2}).");
    }
}
