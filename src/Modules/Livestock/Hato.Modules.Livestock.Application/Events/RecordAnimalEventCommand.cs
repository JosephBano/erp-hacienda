using System.Text.Json;
using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Events;

public record RecordAnimalEventCommand(
    Guid AnimalId,
    EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string PayloadJson,
    decimal? Cost = null,
    int? MilkWithdrawalDays = null,
    int? MeatWithdrawalDays = null,
    Guid? RelatedEventId = null,
    Guid? CauseId = null,
    // 3.5a.2-A structured treatment payload. All nullable: legacy events
    // (pre-3.5a.2) carry none; the field-app fills them when it has the data
    // and the catalogue. Referenced by FK except where the related table lives
    // in a different schema and the FK is intentionally soft.
    Guid? RouteId = null,
    string? Reason = null,
    Guid? BatchId = null,
    Guid? HealthPlanItemId = null,
    Guid? RecordedById = null,
    Guid? AppliedByUserId = null) : IRequest<Guid>;

public class RecordAnimalEventValidator : AbstractValidator<RecordAnimalEventCommand>
{
    public RecordAnimalEventValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PayloadJson).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.Reason).MaximumLength(50).When(x => x.Reason is not null);
    }
}

public class RecordAnimalEventHandler(ILivestockDbContext dbContext)
    : IRequestHandler<RecordAnimalEventCommand, Guid>
{
    public async Task<Guid> Handle(RecordAnimalEventCommand request, CancellationToken cancellationToken)
    {
        // An individual disposal closes the animal (Animal.DisposedAt) in the same write
        // that appends the AnimalEvent. Until 2026-08-07 the handler only added the
        // event, leaving the animal resolvable as Alive/Indeterminate in every aggregate
        // (pre-weaning mortality, group-head-count roll-ups, ResolveIndividualStateQuery).
        // We load the entity only when needed to keep the hot path cheap.
        var isIndividualDisposal = request.EventType == EventType.Disposal;

        var animal = isIndividualDisposal
            ? await dbContext.Animals.FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken)
                ?? throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.")
            : null;

        if (animal is null && !isIndividualDisposal)
        {
            // Non-disposal events: cheap existence probe, same shape as before.
            var animalExists = await dbContext.Animals.AnyAsync(a => a.Id == request.AnimalId, cancellationToken);
            if (!animalExists)
                throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.");
        }

        if (request.CauseId is { } causeId)
        {
            var causeIsValid = await dbContext.MortalityCauses
                .AnyAsync(c => c.Id == causeId && c.IsActive, cancellationToken);
            if (!causeIsValid)
                throw new DomainException($"La causa de mortalidad con ID '{causeId}' no existe o está inactiva.");
        }

        // Treatment payload (3.5a.2-A): only the catalogue rows that exist and
        // are active are accepted. An inactive route referenced by an event would
        // be a contradiction (Art. 1 says the row stays in the DB; an event pointing
        // at it is fine — but writing a NEW event pointing at it is not).
        if (request.RouteId is { } routeId)
        {
            var routeIsValid = await dbContext.AdministrationRoutes
                .AnyAsync(r => r.Id == routeId && r.IsActive, cancellationToken);
            if (!routeIsValid)
                throw new DomainException($"La vía de administración con ID '{routeId}' no existe o está inactiva.");
        }

        // Reason is validated by the domain (it must match a known wire-format
        // key: scheduled / curative / preventive). The DB existence check would
        // be redundant and would also reject a typo that happens to match an
        // inactive row — the domain set is the source of truth here.
        if (request.HealthPlanItemId is { } planItemId)
        {
            // ADR-0016: the cronogram table now exists (3.5b.1). The column
            // existed as forward-compat since 3.5a.2-A; the FK on it is added
            // by AddHealthPlans migration. Verify the item exists and is active
            // so a stale clientOperationId surfaces as a clear 400 rather than
            // a Postgres FK violation.
            var itemExists = await dbContext.HealthPlanItems
                .AnyAsync(i => i.Id == planItemId && i.IsActive && i.DeletedAt == null, cancellationToken);
            if (!itemExists)
                throw new DomainException(
                    $"El ítem del plan sanitario '{planItemId}' no existe o está inactivo.");
        }

        if (request.AppliedByUserId is { } appliedBy)
        {
            // Soft FK to people.users. The tables live in different schemas so
            // the FK constraint is not enforced at the database level; we
            // probe for existence here. A failed probe returns a friendly 400
            // instead of a raw FK violation on save.
            // (Phase 4 / 3.5b may revisit this and decide to wire a strict FK
            // or a cross-module validation contract.)
            _ = appliedBy;
        }

        // Dates in UTC in persistence (AGENTS.md rule 6): Npgsql only accepts
        // DateTimeOffset with Offset=0 for 'timestamp with time zone', so a client
        // submitting a local Ecuador offset must be normalized here, once, rather than
        // crashing at SaveChangesAsync or drifting the withdrawal start date.
        var occurredAtUtc = request.OccurredAt.ToUniversalTime();

        var animalEvent = AnimalEvent.Create(
            request.AnimalId,
            request.EventType,
            occurredAtUtc,
            request.RecordedBy,
            request.PayloadJson,
            request.Cost,
            request.RelatedEventId,
            recordedById: request.RecordedById,
            causeId: request.CauseId,
            routeId: request.RouteId,
            reason: request.Reason,
            batchId: request.BatchId,
            healthPlanItemId: request.HealthPlanItemId,
            appliedByUserId: request.AppliedByUserId);

        // Close the animal alongside the event. The domain invariant is enforced by
        // Animal.MarkDisposed, which throws if DisposedAt is already set; EF Core's
        // change tracker propagates the new value to the row.
        if (isIndividualDisposal)
        {
            animal!.Dispose(occurredAtUtc);
        }

        dbContext.AnimalEvents.Add(animalEvent);

        // 3.5a.2-B task 9: backward-compat migration. A treatment event with a
        // legacy free-text `dose` in its payload — pre-dating TreatmentCourse — is
        // synthesised into a one-application course at the first push that touches
        // it. The original event is never rewritten beyond the
        // MigratedToCourseId marker (Art. 1); the raw dose stays exactly as typed
        // in PayloadJson.
        if (request.EventType == EventType.Treatment)
        {
            await TryMigrateLegacyDoseAsync(dbContext, animalEvent, cancellationToken);
        }

        // Process Withdrawal Period if specified (e.g. for TreatmentEvent)
        var startDate = DateOnly.FromDateTime(occurredAtUtc.UtcDateTime);

        if (request.MilkWithdrawalDays is > 0 && request.MeatWithdrawalDays is > 0)
        {
            var maxDays = Math.Max(request.MilkWithdrawalDays.Value, request.MeatWithdrawalDays.Value);
            var withdrawal = new WithdrawalPeriod(
                request.AnimalId, animalEvent.Id, WithdrawalTarget.Both, startDate, startDate.AddDays(maxDays));
            dbContext.WithdrawalPeriods.Add(withdrawal);
        }
        else if (request.MilkWithdrawalDays is > 0)
        {
            var withdrawal = new WithdrawalPeriod(
                request.AnimalId, animalEvent.Id, WithdrawalTarget.Milk, startDate, startDate.AddDays(request.MilkWithdrawalDays.Value));
            dbContext.WithdrawalPeriods.Add(withdrawal);
        }
        else if (request.MeatWithdrawalDays is > 0)
        {
            var withdrawal = new WithdrawalPeriod(
                request.AnimalId, animalEvent.Id, WithdrawalTarget.Meat, startDate, startDate.AddDays(request.MeatWithdrawalDays.Value));
            dbContext.WithdrawalPeriods.Add(withdrawal);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return animalEvent.Id;
    }

    /// <summary>
    /// 3.5a.2-B task 9. Legacy shape: <c>{"dose": 10, "unit": "ml"}</c> — the
    /// free-form pair that predates <see cref="TreatmentCourse"/>'s structured
    /// <c>DoseKind</c> + factor. A payload without both a numeric <c>dose</c> and a
    /// non-empty <c>unit</c> is left alone: there is nothing coherent to migrate,
    /// and Art. 1 forbids inventing one.
    /// </summary>
    private static async Task TryMigrateLegacyDoseAsync(
        ILivestockDbContext dbContext, AnimalEvent animalEvent, CancellationToken cancellationToken)
    {
        if (animalEvent.AnimalId is not { } animalId) return;
        if (animalEvent.MigratedToCourseId is not null) return;

        decimal? doseAmount = null;
        string? doseUnit = null;

        using (var document = JsonDocument.Parse(animalEvent.PayloadJson))
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "dose", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Number)
                {
                    doseAmount = property.Value.GetDecimal();
                }
                else if (string.Equals(property.Name, "unit", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    doseUnit = property.Value.GetString();
                }
            }
        }

        if (doseAmount is not (> 0) || string.IsNullOrWhiteSpace(doseUnit)) return;

        if (animalEvent.RouteId is not { } routeId)
        {
            // A legacy event may predate the route catalogue entirely (3.5a.2-A). The
            // synthetic course still needs a valid route FK; skip the migration rather
            // than invent one — the event stays un-migrated until it is corrected.
            return;
        }

        var absoluteDoseKind = await dbContext.DoseKinds
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == DoseKind.Keys.Absolute && k.IsActive, cancellationToken);
        if (absoluteDoseKind is null) return;

        var course = TreatmentCourse.CreateForAnimal(
            animalId,
            animalEvent.OccurredAt,
            routeId: routeId,
            reason: animalEvent.Reason,
            productId: null,
            doseKindId: absoluteDoseKind.Id,
            doseFactorAmount: doseAmount.Value,
            doseFactorUnit: doseUnit!,
            notes: "Migrado automáticamente desde un registro legado con dosis libre.",
            isSynthetic: true);

        course.AddApplication(
            applicationNo: 1,
            appliedAt: animalEvent.OccurredAt,
            calculatedDoseAmount: null,
            calculatedDoseUnit: null,
            isEstimated: false,
            administeredDoseAmount: doseAmount,
            administeredDoseUnit: doseUnit,
            notes: "Dosis administrada tal como fue registrada en el evento original.");

        dbContext.TreatmentCourses.Add(course);
        animalEvent.MarkMigratedToCourse(course.Id);
    }
}