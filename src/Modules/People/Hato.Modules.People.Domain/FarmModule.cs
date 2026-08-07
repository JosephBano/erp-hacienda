using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

/// <summary>
/// The on/off row for a single module of the farm (ADR-0019).
///
/// The decision belongs to the product owner, not the catalogue of data. A
/// module's <c>enabled</c> flag is the only thing that flips when the owner
/// presses the toggle in the admin-web panel; the phone reads it through the
/// sync pull and evaluates it locally for the navigation visibility decision
/// (Art. 9: no network round-trip decides what the operator sees).
///
/// The seed mirrors the field-app's vocabulary: the keys come from the
/// \`ModuleKey\` union on the client so the two sides agree on identifiers
/// without a translation table. Production is the only one that ships
/// disabled for the pilot — the rest default to enabled until the owner
/// turns them off explicitly.
/// </summary>
public class FarmModule : AuditableEntity
{
    public string Key { get; private set; }
    public bool Enabled { get; private set; }
    public string? DisabledReason { get; private set; }

    private FarmModule()
    {
        Key = null!;
    }

    private FarmModule(string key, bool enabled, string? disabledReason)
    {
        Key = key;
        Enabled = enabled;
        DisabledReason = disabledReason;
    }

    public static FarmModule Create(string key, bool enabled = true, string? disabledReason = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("La clave del módulo no puede estar vacía.");

        var normalized = key.Trim().ToLowerInvariant();
        return new FarmModule(normalized, enabled, disabledReason?.Trim());
    }

    /// <summary>
    /// Flips the on/off switch. The reason is required when disabling so the
    /// audit trail of the panel reads as a sentence, not a missing field.
    /// </summary>
    public void SetEnabled(bool enabled, string? disabledReason = null)
    {
        if (!enabled && string.IsNullOrWhiteSpace(disabledReason))
        {
            throw new DomainException(
                "Apagar un módulo requiere una razón; el campo no puede quedar vacío.");
        }

        Enabled = enabled;
        DisabledReason = enabled ? null : disabledReason!.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
