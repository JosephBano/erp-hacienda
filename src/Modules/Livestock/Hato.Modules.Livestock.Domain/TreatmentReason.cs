using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Configurable catalog of why a treatment was applied
/// (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-A): distinguishing
/// "tocaba por cronograma" (<see cref="Scheduled"/>) from "curé algo"
/// (<see cref="Curative"/>) from "preventivo fuera de cronograma"
/// (<see cref="Preventive"/>) is what separates "vacuna de calendario" from
/// "vacuna porque se enfermó" — and that distinction is what the panel
/// wants to see on the herd's history.
/// </summary>
public class TreatmentReason : AuditableEntity
{
    /// <summary>Stable identifier on the wire. Lower-snake-case, no whitespace.</summary>
    public string Key { get; private set; }

    /// <summary>Visible label, Spanish.</summary>
    public string LabelEs { get; private set; }

    /// <summary>Soft-delete flag. Inactive reasons are filtered out of the
    /// field-app's drop-down but the row stays in the database so historical
    /// <c>TreatmentEvent</c> references remain valid (Art. 1).</summary>
    public bool IsActive { get; private set; }

    private TreatmentReason() { Key = null!; LabelEs = null!; }

    private TreatmentReason(string key, string labelEs)
    {
        Key = key;
        LabelEs = labelEs;
        IsActive = true;
    }

    public static TreatmentReason Create(string key, string labelEs)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("La clave del motivo de tratamiento no puede estar vacía.");

        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta del motivo de tratamiento no puede estar vacía.");

        var normalisedKey = NormaliseKey(key);
        if (normalisedKey is null)
            throw new DomainException(
                "La clave del motivo debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        var trimmedLabel = labelEs.Trim();
        if (trimmedLabel.Length > 100)
            throw new DomainException("La etiqueta del motivo no puede superar 100 caracteres.");

        return new TreatmentReason(normalisedKey, trimmedLabel);
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("El motivo de tratamiento ya está inactivo.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("El motivo de tratamiento ya está activo.");

        IsActive = true;
    }

    public void UpdateLabel(string labelEs)
    {
        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta del motivo de tratamiento no puede estar vacía.");

        LabelEs = labelEs.Trim();
    }

    private static string? NormaliseKey(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 50)
            return null;

        return Regex.IsMatch(trimmed, "^[a-z0-9_]+$") ? trimmed : null;
    }
}