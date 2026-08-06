namespace Hato.Modules.People.Domain;

/// <summary>
/// Immutable audit log entry for employee activity and system actions (Art. 1 + Art. 4 + LOPDP).
/// Cannot be modified or deleted.
/// </summary>
public class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? UserFullName { get; private set; }
    public string Action { get; private set; }
    public string Module { get; private set; }
    public string EntityName { get; private set; }
    public string EntityId { get; private set; }
    public string? DetailsJson { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    private AuditLog()
    {
        Action = null!;
        Module = null!;
        EntityName = null!;
        EntityId = null!;
    }

    private AuditLog(
        Guid? userId,
        string? userEmail,
        string? userFullName,
        string action,
        string module,
        string entityName,
        string entityId,
        string? detailsJson,
        DateTimeOffset timestamp)
    {
        UserId = userId;
        UserEmail = userEmail;
        UserFullName = userFullName;
        Action = action;
        Module = module;
        EntityName = entityName;
        EntityId = entityId;
        DetailsJson = detailsJson;
        Timestamp = timestamp;
    }

    public static AuditLog Create(
        Guid? userId,
        string? userEmail,
        string? userFullName,
        string action,
        string module,
        string entityName,
        string entityId,
        string? detailsJson = null,
        DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("La acción es requerida.", nameof(action));

        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("El nombre de la entidad es requerido.", nameof(entityName));

        return new AuditLog(
            userId,
            userEmail?.Trim(),
            userFullName?.Trim(),
            action.Trim(),
            module?.Trim() ?? "General",
            entityName.Trim(),
            entityId.Trim(),
            detailsJson,
            timestamp ?? DateTimeOffset.UtcNow);
    }
}
