namespace Hato.Modules.People.Domain;

/// <summary>
/// Conflict log entry for the sync protocol's LWW resolution (ADR-0008): "para entidades
/// editables, last-write-wins por campo + bitácora de conflictos revisable en el panel".
///
/// One row per field that genuinely conflicted — a device's edit landed on top of a value
/// another write had already changed since that device last knew about the record. It is
/// immutable and never deleted (Art. 1 + Art. 4): this is the audit trail an admin uses to
/// notice a field that keeps getting fought over between two employees' phones.
/// </summary>
public class SyncConflict
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string FieldName { get; private set; }
    public string? ServerValue { get; private set; }
    public string? AttemptedValue { get; private set; }
    public string Resolution { get; private set; }
    public Guid? ClientOperationId { get; private set; }
    public string? DeviceId { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }

    private SyncConflict()
    {
        EntityType = null!;
        FieldName = null!;
        Resolution = null!;
    }

    private SyncConflict(
        string entityType,
        Guid entityId,
        string fieldName,
        string? serverValue,
        string? attemptedValue,
        SyncConflictResolution resolution,
        Guid? clientOperationId,
        string? deviceId,
        DateTimeOffset detectedAt)
    {
        EntityType = entityType;
        EntityId = entityId;
        FieldName = fieldName;
        ServerValue = serverValue;
        AttemptedValue = attemptedValue;
        Resolution = resolution.ToString();
        ClientOperationId = clientOperationId;
        DeviceId = deviceId;
        DetectedAt = detectedAt;
    }

    public static SyncConflict Create(
        string entityType,
        Guid entityId,
        string fieldName,
        string? serverValue,
        string? attemptedValue,
        SyncConflictResolution resolution,
        Guid? clientOperationId = null,
        string? deviceId = null,
        DateTimeOffset? detectedAt = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("El tipo de entidad es requerido.", nameof(entityType));

        if (entityId == Guid.Empty)
            throw new ArgumentException("El conflicto debe referenciar una entidad válida.", nameof(entityId));

        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("El campo en conflicto es requerido.", nameof(fieldName));

        return new SyncConflict(
            entityType.Trim(),
            entityId,
            fieldName.Trim(),
            serverValue,
            attemptedValue,
            resolution,
            clientOperationId,
            deviceId?.Trim(),
            detectedAt ?? DateTimeOffset.UtcNow);
    }
}

public enum SyncConflictResolution
{
    /// <summary>The incoming write's value was applied (its declared moment was later).</summary>
    ClientWon,

    /// <summary>The value already on the server was kept (it was written more recently).</summary>
    ServerWon,
}
