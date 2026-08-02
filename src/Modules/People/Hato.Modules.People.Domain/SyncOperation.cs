using Hato.SharedKernel;

namespace Hato.Modules.People.Domain;

public enum SyncOperationStatus
{
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
        Status = SyncOperationStatus.Accepted;
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
