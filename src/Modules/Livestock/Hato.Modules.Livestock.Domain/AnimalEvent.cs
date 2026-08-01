using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// An immutable, dated event in the life of an animal or group (Art. 1 + Art. 4 + ARCHITECTURE.md).
/// History is append-only: corrections are recorded as new events referencing the original.
/// Payload is stored as JSONB for typed event data.
/// </summary>
public class AnimalEvent : AuditableEntity
{
    public Guid AnimalId { get; private set; }
    public EventType EventType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string RecordedBy { get; private set; }
    public decimal? Cost { get; private set; }
    public string PayloadJson { get; private set; }
    public Guid? RelatedEventId { get; private set; }

    private AnimalEvent()
    {
        RecordedBy = null!;
        PayloadJson = null!;
    }

    private AnimalEvent(
        Guid animalId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedBy,
        string payloadJson,
        decimal? cost,
        Guid? relatedEventId)
    {
        AnimalId = animalId;
        EventType = eventType;
        OccurredAt = occurredAt;
        RecordedBy = recordedBy;
        PayloadJson = payloadJson;
        Cost = cost;
        RelatedEventId = relatedEventId;
    }

    public static AnimalEvent Create(
        Guid animalId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedBy,
        string payloadJson,
        decimal? cost = null,
        Guid? relatedEventId = null)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("Un evento debe estar asociado a un animal.");

        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El autor del registro no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("El contenido del evento no puede estar vacío.");

        if (cost is < 0)
            throw new DomainException("El costo de un evento no puede ser negativo.");

        return new AnimalEvent(
            animalId,
            eventType,
            occurredAt,
            recordedBy.Trim(),
            payloadJson.Trim(),
            cost,
            relatedEventId);
    }
}
