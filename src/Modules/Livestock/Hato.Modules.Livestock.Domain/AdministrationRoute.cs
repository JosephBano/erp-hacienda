using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Configurable catalog of how a treatment is delivered to an animal
/// (PLAN-FASE-3-5-PORCINO-3.5a.2-A): oral in water, oral in feed, intramuscular,
/// subcutaneous, topical, intranasal, intrauterine. The list lives in the
/// database, not in an enum (Art. 8): a new route the client adopts is an INSERT,
/// never a deploy.
///
/// <para>
/// <see cref="Key"/> is the wire-format identifier shared with the field-app
/// (sync pull, drop-down lookups). It is normalised to lower_snake_case and
/// rejected if it has whitespace, so a typo in the panel cannot produce two
/// routes that look the same to a human and different to a parser.
/// </para>
///
/// <para>
/// Rows are deactivated, never deleted (Art. 1): a route already referenced by a
/// historical <c>TreatmentEvent</c> stays readable on that event, even after the
/// panel retires it from new use.
/// </para>
/// </summary>
public class AdministrationRoute : AuditableEntity
{
    /// <summary>
    /// Stable identifier on the wire. Lower-snake-case, no whitespace.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// Visible label, Spanish. Free text within length limits.
    /// </summary>
    public string LabelEs { get; private set; }

    /// <summary>
    /// Soft-delete flag. Inactive routes are filtered out of the panel's
    /// "create new event" drop-down but the row stays in the database so
    /// historical <c>TreatmentEvent.route_id</c> references remain valid.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Forward-compat with <c>units</c>: a hint for the field-app about which
    /// unit base to expect for <see cref="TreatmentEvent"/> payloads of this
    /// route. Nullable because the units catalog does not exist yet
    /// (3.5a.2-B will land it).
    /// </summary>
    public Guid? DefaultUnitId { get; private set; }

    private AdministrationRoute() { Key = null!; LabelEs = null!; }

    private AdministrationRoute(string key, string labelEs)
    {
        Key = key;
        LabelEs = labelEs;
        IsActive = true;
    }

    public static AdministrationRoute Create(string key, string labelEs)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("La clave de la vía de administración no puede estar vacía.");

        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta de la vía de administración no puede estar vacía.");

        var normalisedKey = NormaliseKey(key);
        if (normalisedKey is null)
            throw new DomainException(
                "La clave de la vía debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        var trimmedLabel = labelEs.Trim();
        if (trimmedLabel.Length > 100)
            throw new DomainException("La etiqueta de la vía no puede superar 100 caracteres.");

        return new AdministrationRoute(normalisedKey, trimmedLabel);
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("La vía de administración ya está inactiva.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("La vía de administración ya está activa.");

        IsActive = true;
    }

    public void UpdateLabel(string labelEs)
    {
        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta de la vía de administración no puede estar vacía.");

        LabelEs = labelEs.Trim();
    }

    public void SetDefaultUnit(Guid? unitId)
    {
        DefaultUnitId = unitId;
    }

    private static string? NormaliseKey(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 50)
            return null;

        // Lower-snake-case only. Reject whitespace, dashes, uppercase, accents.
        // The pattern permits letters (incl. Spanish ñ/á in lower-case already
        // because we lowered), digits and underscore, from start to end.
        return Regex.IsMatch(trimmed, "^[a-z0-9_]+$") ? trimmed : null;
    }
}