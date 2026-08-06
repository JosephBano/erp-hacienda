using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// An immutable, dated event in the life of an animal or a group (Art. 1 + Art. 4 +
/// ARCHITECTURE.md). History is append-only: corrections are recorded as new events
/// referencing the original. Payload is stored as JSONB for typed event data.
///
/// The subject is exactly one of <see cref="AnimalId"/> / <see cref="GroupId"/> (ADR-0015
/// sec.2): "vaccinated this animal" and "vaccinated this lot" are the same
/// <see cref="EventType"/> with a different subject, not two event types. A group event is
/// never materialized per member — that would invent which individual it happened to.
/// </summary>
public class AnimalEvent : AuditableEntity
{
    public Guid? AnimalId { get; private set; }
    public Guid? GroupId { get; private set; }
    public EventType EventType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? RecordedById { get; private set; }
    public string RecordedByLabel { get; private set; }
    public string RecordedBy => RecordedByLabel;
    public decimal? Cost { get; private set; }
    public string PayloadJson { get; private set; }
    public Guid? RelatedEventId { get; private set; }

    /// <summary>
    /// How many head a group-subject event is about — the one number a query needs to
    /// aggregate (e.g. <c>LiveHeadCount</c>) without parsing <see cref="PayloadJson"/>.
    /// Null for animal-subject events; required and strictly positive whenever
    /// <see cref="GroupId"/> is set and the event is a disposal (ADR-0015 sec.4/7).
    /// </summary>
    public int? AffectedCount { get; private set; }

    private AnimalEvent()
    {
        RecordedByLabel = null!;
        PayloadJson = null!;
    }

    private AnimalEvent(
        Guid? animalId,
        Guid? groupId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedByLabel,
        Guid? recordedById,
        string payloadJson,
        decimal? cost,
        Guid? relatedEventId,
        int? affectedCount)
    {
        AnimalId = animalId;
        GroupId = groupId;
        EventType = eventType;
        OccurredAt = occurredAt;
        RecordedByLabel = recordedByLabel;
        RecordedById = recordedById;
        PayloadJson = payloadJson;
        Cost = cost;
        RelatedEventId = relatedEventId;
        AffectedCount = affectedCount;
    }

    /// <summary>Records an event whose subject is a single, identifiable animal.</summary>
    public static AnimalEvent Create(
        Guid animalId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedBy,
        string payloadJson,
        decimal? cost = null,
        Guid? relatedEventId = null,
        Guid? recordedById = null)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("Un evento individual debe estar asociado a un animal.");

        return CreateInternal(
            animalId, null, eventType, occurredAt, recordedBy, payloadJson,
            cost, relatedEventId, recordedById, affectedCount: null);
    }

    /// <summary>
    /// Records an event whose subject is a group by count (ADR-0015). "The lot ate 3 sacks"
    /// or "12 were sold" never resolve to which animal — that is exactly the anonymity the
    /// lot is declaring. <paramref name="affectedCount"/> carries how many head the event
    /// is about when that number matters to a downstream query (disposals); it is optional
    /// otherwise (e.g. a sample weighing, whose sample size lives in the payload).
    /// </summary>
    public static AnimalEvent CreateForGroup(
        Guid groupId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedBy,
        string payloadJson,
        int? affectedCount = null,
        decimal? cost = null,
        Guid? relatedEventId = null,
        Guid? recordedById = null)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("Un evento grupal debe estar asociado a un lote.");

        return CreateInternal(
            null, groupId, eventType, occurredAt, recordedBy, payloadJson,
            cost, relatedEventId, recordedById, affectedCount);
    }

    private static AnimalEvent CreateInternal(
        Guid? animalId,
        Guid? groupId,
        EventType eventType,
        DateTimeOffset occurredAt,
        string recordedBy,
        string payloadJson,
        decimal? cost,
        Guid? relatedEventId,
        Guid? recordedById,
        int? affectedCount)
    {
        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El autor del registro no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("El contenido del evento no puede estar vacío.");

        if (cost is < 0)
            throw new DomainException("El costo de un evento no puede ser negativo.");

        if (affectedCount is <= 0)
            throw new DomainException("La cantidad de cabezas afectadas debe ser mayor a cero.");

        return new AnimalEvent(
            animalId,
            groupId,
            eventType,
            occurredAt,
            recordedBy.Trim(),
            recordedById,
            payloadJson.Trim(),
            cost,
            relatedEventId,
            affectedCount);
    }
}
