using System.Text.RegularExpressions;
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
    // The set of treatment reasons the catalogue seeds (3.5a.2-A). These are
    // the wire-format keys the field-app sends on the structured treatment
    // payload — typed here so a typo in the panel never produces a reason that
    // looks like a valid one to a parser.
    private static readonly HashSet<string> KnownTreatmentReasons = new(StringComparer.Ordinal)
    {
        "scheduled",
        "curative",
        "preventive",
    };

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

    /// <summary>
    /// Which <see cref="MortalityCause"/> this event declares — set on a disposal that is
    /// a death, individual or group (ADR-0015: "DisposalType.Death gana cause_id, y
    /// GroupMortality lo lleva también"). Optional: a sale or an unwitnessed disposal
    /// legitimately carries none. Referential validity (the row exists and is active) is
    /// an Application-layer check, not a domain invariant — the domain has no database.
    /// </summary>
    public Guid? CauseId { get; private set; }

    /// <summary>
    /// Vía de administración del tratamiento (3.5a.2-A). FK a
    /// <see cref="AdministrationRoute"/>. Nullable: a treatment event without a route
    /// is a legacy record from before the catalogue existed; new events with
    /// <see cref="EventType"/> ∈ {Treatment, Vaccination} on this farm should
    /// populate it. Referential validity is an Application-layer check (the row exists
    /// and is active).
    /// </summary>
    public Guid? RouteId { get; private set; }

    /// <summary>
    /// Why the treatment was applied (3.5a.2-A): one of <c>scheduled</c> /
    /// <c>curative</c> / <c>preventive</c>. Stored as the catalogue's wire-format
    /// key, lower-snake-case, normalised silently from upper-case. Nullable for the
    /// same reason as <see cref="RouteId"/>.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Lote de inventario del producto aplicado (3.5a.2-A). FK suave a
    /// <c>inventory_batches</c> — soft FK porque el catálogo de inventory no comparte
    /// esquema con livestock y la FK cross-schema queda pendiente hasta Fase 4. Nullable.
    /// </summary>
    public Guid? BatchId { get; private set; }

    /// <summary>
    /// Forward-compat con el cronograma configurable (ADR-0016, 3.5b.1). Nullable.
    /// La columna existe desde 3.5a.2-A; la FK a <c>health_plan_items</c> llega
    /// cuando el cronograma exista. No poblar este campo sin tener la tabla lista:
    /// un valor suelto aquí no rompe nada hoy, pero el cronograma de 3.5b.1 lo
    /// va a ignorar si no referencia un ítem conocido.
    /// </summary>
    public Guid? HealthPlanItemId { get; private set; }

    /// <summary>
    /// Quién aplicó el tratamiento (veterinario / técnico / operario). FK suave a
    /// <c>people.users</c>. Distinta de <see cref="RecordedById"/>: el que tipeó el
    /// registro puede ser distinto del que físicamente lo aplicó. El plan es
    /// explícito (3.5a.2-A sec."Tareas" punto 3): "applied_by ≠ recorded_by por
    /// nulabilidad, no por desigualdad semántica". Las dos FKs conviven; el
    /// sistema <strong>no</strong> asume que son la misma persona.
    /// </summary>
    public Guid? AppliedByUserId { get; private set; }

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
        int? affectedCount,
        Guid? causeId,
        Guid? routeId,
        string? reason,
        Guid? batchId,
        Guid? healthPlanItemId,
        Guid? appliedByUserId)
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
        CauseId = causeId;
        RouteId = routeId;
        Reason = reason;
        BatchId = batchId;
        HealthPlanItemId = healthPlanItemId;
        AppliedByUserId = appliedByUserId;
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
        Guid? recordedById = null,
        Guid? causeId = null,
        Guid? routeId = null,
        string? reason = null,
        Guid? batchId = null,
        Guid? healthPlanItemId = null,
        Guid? appliedByUserId = null)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("Un evento individual debe estar asociado a un animal.");

        return CreateInternal(
            animalId, null, eventType, occurredAt, recordedBy, payloadJson,
            cost, relatedEventId, recordedById, affectedCount: null, causeId,
            routeId, reason, batchId, healthPlanItemId, appliedByUserId);
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
        Guid? recordedById = null,
        Guid? causeId = null,
        Guid? routeId = null,
        string? reason = null,
        Guid? batchId = null,
        Guid? healthPlanItemId = null,
        Guid? appliedByUserId = null)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("Un evento grupal debe estar asociado a un lote.");

        return CreateInternal(
            null, groupId, eventType, occurredAt, recordedBy, payloadJson,
            cost, relatedEventId, recordedById, affectedCount, causeId,
            routeId, reason, batchId, healthPlanItemId, appliedByUserId);
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
        int? affectedCount,
        Guid? causeId,
        Guid? routeId,
        string? reason,
        Guid? batchId,
        Guid? healthPlanItemId,
        Guid? appliedByUserId)
    {
        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El autor del registro no puede estar vacío.");

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("El contenido del evento no puede estar vacío.");

        if (cost is < 0)
            throw new DomainException("El costo de un evento no puede ser negativo.");

        if (affectedCount is <= 0)
            throw new DomainException("La cantidad de cabezas afectadas debe ser mayor a cero.");

        if (causeId == Guid.Empty)
            throw new DomainException("La causa, si se declara, debe ser válida.");

        if (routeId == Guid.Empty)
            throw new DomainException("La vía de administración, si se declara, debe ser válida.");

        if (batchId == Guid.Empty)
            throw new DomainException("El lote de inventario, si se declara, debe ser válido.");

        if (healthPlanItemId == Guid.Empty)
            throw new DomainException("El ítem del plan sanitario, si se declara, debe ser válido.");

        if (appliedByUserId == Guid.Empty)
            throw new DomainException("El aplicador, si se declara, debe ser válido.");

        var normalisedReason = NormaliseReason(reason);

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
            affectedCount,
            causeId,
            routeId,
            normalisedReason,
            batchId,
            healthPlanItemId,
            appliedByUserId);
    }

    private static string? NormaliseReason(string? raw)
    {
        if (raw is null) return null;

        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return null;

        // Lower-case silently so "Curative" and "CURATIVE" both normalise to
        // "curative" — same rule as the TreatmentReason catalogue (Art. 8
        // hygiene: a typo in the panel that adds whitespace or dashes is
        // rejected, but a casing difference is not).
        var lowered = trimmed.ToLowerInvariant();

        if (!Regex.IsMatch(lowered, "^[a-z0-9_]+$"))
            throw new DomainException(
                "El motivo del tratamiento debe estar en minúsculas, sin espacios y solo con letras, dígitos y guion bajo.");

        if (!KnownTreatmentReasons.Contains(lowered))
            throw new DomainException(
                $"El motivo '{lowered}' no existe en el catálogo. Use uno de: scheduled, curative, preventive.");

        return lowered;
    }
}