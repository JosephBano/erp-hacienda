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
}