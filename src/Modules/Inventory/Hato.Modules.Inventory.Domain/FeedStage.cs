using System.Text.RegularExpressions;
using Hato.SharedKernel;

namespace Hato.Modules.Inventory.Domain;

/// <summary>
/// Configurable catalog of feeding stages (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5 task 3):
/// preiniciador, iniciador, crecimiento, engorde, gestación, lactancia. The list lives in
/// the database, not in an enum (Art. 8): a farm that renames or adds a stage does an
/// INSERT, never a deploy.
///
/// <para>
/// <see cref="Key"/> is the wire-format identifier shared with other clients (sync pull,
/// drop-down lookups), same convention as <c>AdministrationRoute</c>/<c>TreatmentReason</c>:
/// normalised to lower_snake_case, rejected if it has whitespace.
/// </para>
///
/// <para>
/// Rows are deactivated, never deleted (Art. 1): a stage already referenced by an
/// <c>InventoryItem</c> stays readable on that item even after the panel retires it from
/// new use.
/// </para>
/// </summary>
public class FeedStage : AuditableEntity
{
    /// <summary>Stable identifier on the wire. Lower-snake-case, no whitespace.</summary>
    public string Key { get; private set; }

    /// <summary>Visible label, Spanish. Free text within length limits.</summary>
    public string LabelEs { get; private set; }

    /// <summary>
    /// Soft-delete flag. Inactive stages are filtered out of the "create/edit item"
    /// drop-down but the row stays in the database so historical
    /// <c>InventoryItem.FeedStageId</c> references remain valid.
    /// </summary>
    public bool IsActive { get; private set; }

    private FeedStage() { Key = null!; LabelEs = null!; }

    private FeedStage(string key, string labelEs)
    {
        Key = key;
        LabelEs = labelEs;
        IsActive = true;
    }

    public static FeedStage Create(string key, string labelEs)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("La clave de la etapa de alimento no puede estar vacía.");

        if (string.IsNullOrWhiteSpace(labelEs))
            throw new DomainException("La etiqueta de la etapa de alimento no puede estar vacía.");

        var normalisedKey = NormaliseKey(key);
        if (normalisedKey is null)
            throw new DomainException(
                "La clave de la etapa debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        var trimmedLabel = labelEs.Trim();
        if (trimmedLabel.Length > 100)
            throw new DomainException("La etiqueta de la etapa no puede superar 100 caracteres.");

        return new FeedStage(normalisedKey, trimmedLabel);
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("La etapa de alimento ya está inactiva.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("La etapa de alimento ya está activa.");

        IsActive = true;
    }

    private static string? NormaliseKey(string raw)
    {
        var trimmed = raw.Trim().ToLowerInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 50)
            return null;

        return Regex.IsMatch(trimmed, "^[a-z0-9_]+$") ? trimmed : null;
    }
}
