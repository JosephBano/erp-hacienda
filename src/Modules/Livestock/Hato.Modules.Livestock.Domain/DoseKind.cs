using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// How a treatment dose is expressed (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-B task 1):
/// <c>absolute</c> (a fixed quantity, e.g. 10 ml), <c>per_weight</c> (a rate per
/// kilogram, resolved against the last weighing) or <c>per_head</c> (a flat dose
/// repeated per animal/head).
///
/// <para>
/// A table, not a C# enum (Art. 8): the sub-plan is explicit about this even
/// though the set is closed today — it keeps the same catalog shape as
/// <see cref="AdministrationRoute"/> and <see cref="TreatmentReason"/>, and a
/// future fourth form (e.g. a per-surface-area dose for topical treatments)
/// becomes an INSERT instead of a deploy.
/// </para>
/// </summary>
public class DoseKind : AuditableEntity
{
    /// <summary>Stable identifier on the wire. Lower-snake-case, no whitespace.</summary>
    public string Key { get; private set; }

    /// <summary>Visible label, Spanish.</summary>
    public string LabelEs { get; private set; }

    /// <summary>Soft-delete flag (Art. 1): rows stay so historical
    /// <see cref="TreatmentCourse"/> references remain valid.</summary>
    public bool IsActive { get; private set; }

    private DoseKind() { Key = null!; LabelEs = null!; }

    private DoseKind(string key, string labelEs)
    {
        Key = key;
        LabelEs = labelEs;
        IsActive = true;
    }

    public static DoseKind Create(string key, string labelEs)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("La clave de la forma de dosis no puede estar vacía.");

        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta de la forma de dosis no puede estar vacía.");

        var normalisedKey = NormaliseKey(key);
        if (normalisedKey is null)
            throw new DomainException(
                "La clave de la forma de dosis debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        var trimmedLabel = labelEs.Trim();
        if (trimmedLabel.Length > 100)
            throw new DomainException("La etiqueta de la forma de dosis no puede superar 100 caracteres.");

        return new DoseKind(normalisedKey, trimmedLabel);
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("La forma de dosis ya está inactiva.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("La forma de dosis ya está activa.");

        IsActive = true;
    }

    private static string? NormaliseKey(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 50)
            return null;

        return Regex.IsMatch(trimmed, "^[a-z0-9_]+$") ? trimmed : null;
    }

    /// <summary>Wire-format keys seeded by the migration (3.5a.2-B). Referenced by
    /// the dose resolution logic in <c>Application.TreatmentCourses</c> to decide
    /// which formula applies — the three keys are the entire closed set today.</summary>
    public static class Keys
    {
        public const string Absolute = "absolute";
        public const string PerWeight = "per_weight";
        public const string PerHead = "per_head";
    }
}
