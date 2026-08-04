using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

public enum SyncOperationStatus
{
    /// <summary>
    /// Claimed but not yet resolved. The row is written *before* the business command
    /// runs so that the unique index on <c>client_operation_id</c> — not a prior SELECT —
    /// is what rejects a concurrent retry of the same operation. An operation left in
    /// this state means the server died mid-flight: it must surface in the problems tray,
    /// never be replayed blindly.
    /// </summary>
    Processing,
    Accepted,
    Duplicate,
    Rejected
}

public class SyncOperation : Entity
{
    public Guid ClientOperationId { get; private set; }
    public Guid UserId { get; private set; }
    public string? DeviceId { get; private set; }
    public string OperationType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public SyncOperationStatus Status { get; private set; }
    public string? ResultRef { get; private set; }
    public string? ErrorDetails { get; private set; }

    private SyncOperation()
    {
        OperationType = null!;
        PayloadJson = null!;
    }

    public SyncOperation(
        Guid clientOperationId,
        Guid userId,
        string? deviceId,
        string operationType,
        string payloadJson,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt)
    {
        if (clientOperationId == Guid.Empty)
            throw new DomainException("El ID de operación del cliente no puede estar vacío.");

        if (userId == Guid.Empty)
            throw new DomainException("El usuario de la operación no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(operationType))
            throw new DomainException("El tipo de operación es obligatorio.");

        ClientOperationId = clientOperationId;
        UserId = userId;
        DeviceId = deviceId;
        OperationType = operationType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
        Status = SyncOperationStatus.Processing;
    }

    public void MarkAccepted(string? resultRef = null)
    {
        Status = SyncOperationStatus.Accepted;
        ResultRef = resultRef;
        ErrorDetails = null;
    }

    public void MarkDuplicate(string? previousResultRef = null)
    {
        Status = SyncOperationStatus.Duplicate;
        ResultRef = previousResultRef;
    }

    public void MarkRejected(string errorDetails)
    {
        Status = SyncOperationStatus.Rejected;
        ErrorDetails = errorDetails;
    }
}
