using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

/// <summary>
/// 3.5a.2-A second half: the structured treatment payload (route + reason +
/// batch + applied_by + forward-compat health_plan_item_id). docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-A.md
/// sec."Tareas" punto 3, mirrored from the macro plan sec.3.5a.2.
///
/// The fields are added as typed columns next to the existing JSONB payload,
/// not as nested JSON, because they are queryable (withdrawal logic asks
/// "what treatment was this?"; the KPI "treatment by reason" asks "how many
/// curative events in the period?"; the field-app asks "what routes does the
/// catalogue expose?"). JSONB is for the parts no query cares about.
/// </summary>
public class AnimalEventTreatmentPayloadTests
{
    [Fact]
    public void Create_TreatmentEvent_StoresRouteIdReasonAndAppliedBy()
    {
        var animalId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var appliedById = Guid.NewGuid();
        var recordedById = Guid.NewGuid();

        var evt = AnimalEvent.Create(
            animalId,
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            recordedBy: "operario",
            payloadJson: "{}",
            routeId: routeId,
            reason: "curative",
            appliedByUserId: appliedById,
            recordedById: recordedById);

        Assert.Equal(routeId, evt.RouteId);
        Assert.Equal("curative", evt.Reason);
        Assert.Equal(appliedById, evt.AppliedByUserId);
        Assert.Equal(recordedById, evt.RecordedById);
    }

    [Fact]
    public void Create_WeighingEvent_DoesNotRequireRouteOrReason()
    {
        // A weighing does not need a route or reason — they are nullable
        // fields. Only the columns exist; only Treatment / Vaccination /
        // Diagnosis are the events the plan names that populate them.
        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Weighing,
            DateTimeOffset.UtcNow,
            "ordeñador",
            "{\"weightKg\": 450}");

        Assert.Null(evt.RouteId);
        Assert.Null(evt.Reason);
        Assert.Null(evt.AppliedByUserId);
        Assert.Null(evt.HealthPlanItemId);
        Assert.Null(evt.BatchId);
    }

    [Fact]
    public void Create_WithEmptyRouteId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                routeId: Guid.Empty));
    }

    [Fact]
    public void Create_WithEmptyBatchId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                batchId: Guid.Empty));
    }

    [Fact]
    public void Create_WithEmptyHealthPlanItemId_Throws()
    {
        // Forward-compat with ADR-0016 (cronograma). The column exists now,
        // nullable; the FK to the cronogram table lands in 3.5b.1. Empty
        // GUID means "I tried to populate but forgot the value" — reject it.
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                healthPlanItemId: Guid.Empty));
    }

    [Fact]
    public void Create_WithEmptyAppliedByUserId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                appliedByUserId: Guid.Empty));
    }

    [Theory]
    [InlineData("scheduled")]
    [InlineData("curative")]
    [InlineData("preventive")]
    public void Create_WithKnownReason_IsAccepted(string reason)
    {
        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "operario",
            "{}",
            reason: reason);

        Assert.Equal(reason, evt.Reason);
    }

    [Theory]
    [InlineData("SCHEDULED")]
    [InlineData("Curative")]
    public void Create_WithUpperCaseReason_NormalisesToLowerSnake(string reason)
    {
        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "operario",
            "{}",
            reason: reason);

        Assert.Equal(reason.ToLowerInvariant(), evt.Reason);
    }

    [Theory]
    [InlineData("scheduled care")]
    [InlineData("curative-care")]
    [InlineData("scheduled!")]
    [InlineData("unknown_reason")]
    public void Create_WithUnknownOrInvalidReason_Throws(string reason)
    {
        // Whitespace and dashes are rejected by the same rule as the catalog:
        // the reason is the wire-format identifier and a typo in the panel
        // cannot produce two reasons that look the same to a human and
        // different to a parser. Empty / whitespace-only is treated as "no
        // reason" — Reason is optional, see Create_WithoutReason_IsLegal.
        Assert.Throws<DomainException>(() =>
            AnimalEvent.Create(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                reason: reason));
    }

    [Fact]
    public void Create_WithoutReason_IsLegal()
    {
        // Reason is optional — not every event type needs it. A weighing,
        // a movement, a disposal-with-cause but no-reason all leave it null.
        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Weighing,
            DateTimeOffset.UtcNow,
            "operario",
            "{}");

        Assert.Null(evt.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankReason_IsTreatedAsNoReason(string blank)
    {
        // A panel that ships an empty string by accident must not lose the
        // event — the field is optional, so blank == "no reason provided".
        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "operario",
            "{}",
            reason: blank);

        Assert.Null(evt.Reason);
    }

    [Fact]
    public void Create_AppliedByMayEqualRecordedBy()
    {
        // The plan is explicit: "applied_by ≠ recorded_by por nulabilidad,
        // no por desigualdad semántica". The same person applied and
        // registered — that is the common case on a small farm.
        var userId = Guid.NewGuid();

        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "yo mismo",
            "{}",
            appliedByUserId: userId,
            recordedById: userId);

        Assert.Equal(evt.AppliedByUserId, evt.RecordedById);
    }

    [Fact]
    public void Create_AppliedByAndRecordedByMayBeDistinct()
    {
        // The veterinarian applied the treatment; the operator typed the
        // record. This is the case the two FKs exist for — losing the
        // distinction as a default would erase that information.
        var appliedById = Guid.NewGuid();
        var recordedById = Guid.NewGuid();

        var evt = AnimalEvent.Create(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "operario",
            "{}",
            appliedByUserId: appliedById,
            recordedById: recordedById);

        Assert.NotEqual(evt.AppliedByUserId, evt.RecordedById);
    }

    [Fact]
    public void CreateForGroup_TreatmentEvent_StoresRouteIdReasonAndAppliedBy()
    {
        // Treatments on a lot share the same payload shape as on an animal
        // (ADR-0015: subject changes, the rest is the same).
        var routeId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var appliedById = Guid.NewGuid();

        var evt = AnimalEvent.CreateForGroup(
            Guid.NewGuid(),
            EventType.Treatment,
            DateTimeOffset.UtcNow,
            "operario",
            "{}",
            affectedCount: 12,
            routeId: routeId,
            reason: "scheduled",
            batchId: batchId,
            appliedByUserId: appliedById);

        Assert.Equal(routeId, evt.RouteId);
        Assert.Equal("scheduled", evt.Reason);
        Assert.Equal(batchId, evt.BatchId);
        Assert.Equal(appliedById, evt.AppliedByUserId);
    }

    [Fact]
    public void CreateForGroup_WithEmptyRouteId_Throws()
    {
        Assert.Throws<DomainException>(() =>
            AnimalEvent.CreateForGroup(
                Guid.NewGuid(),
                EventType.Treatment,
                DateTimeOffset.UtcNow,
                "operario",
                "{}",
                routeId: Guid.Empty));
    }
}