using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Events;

/// <summary>
/// Records an event whose subject is a group by count, not an individual animal
/// (ADR-0015). A disposal (<see cref="EventType.Disposal"/>) requires
/// <paramref name="AffectedCount"/>: it is the number the derived <c>LiveHeadCount</c>
/// subtracts, and the number that decides whether this event closes the lot (sec.7).
/// </summary>
public record RecordGroupEventCommand(
    Guid GroupId,
    EventType EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string PayloadJson,
    int? AffectedCount = null,
    decimal? Cost = null,
    Guid? RelatedEventId = null,
    Guid? CauseId = null,
    // 3.5a.2-A structured treatment payload. See RecordAnimalEventCommand
    // for the rationale on each field and on which FKs are soft.
    Guid? RouteId = null,
    string? Reason = null,
    Guid? BatchId = null,
    Guid? HealthPlanItemId = null,
    Guid? RecordedById = null,
    Guid? AppliedByUserId = null) : IRequest<Guid>;

public class RecordGroupEventValidator : AbstractValidator<RecordGroupEventCommand>
{
    public RecordGroupEventValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PayloadJson).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.AffectedCount).GreaterThan(0).When(x => x.AffectedCount.HasValue);
        RuleFor(x => x.AffectedCount)
            .NotNull()
            .WithMessage("Una baja de lote debe declarar cuántas cabezas incluye.")
            .When(x => x.EventType == EventType.Disposal);
        RuleFor(x => x.Reason).MaximumLength(50).When(x => x.Reason is not null);
    }
}

public class RecordGroupEventHandler(ILivestockDbContext dbContext)
    : IRequestHandler<RecordGroupEventCommand, Guid>
{
    public async Task<Guid> Handle(RecordGroupEventCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null)
            throw new DomainException($"El lote con ID '{request.GroupId}' no existe.");

        if (!group.IsActive)
            throw new DomainException("No se pueden registrar eventos sobre un lote inactivo.");

        // Bug from the Fase 3.5 retrospective: the individual handler validates CauseId
        // exists + IsActive; the group handler accepted any Guid and would either silently
        // store a dangling reference or, when the FK to mortality_causes is present, throw
        // an opaque DbUpdateException instead of the domain-level rejection the rest of
        // the codebase uses. Mirror the individual handler's check so the operator sees
        // the same error message regardless of which kind of event they were filing.
        if (request.EventType == EventType.Disposal && request.CauseId is { } causeId)
        {
            var causeIsValid = await dbContext.MortalityCauses
                .AnyAsync(c => c.Id == causeId && c.IsActive, cancellationToken);
            if (!causeIsValid)
            {
                throw new DomainException(
                    $"La causa de mortalidad con ID '{causeId}' no existe o está inactiva.");
            }
        }

        // Treatment payload (3.5a.2-A): same validation as the individual handler.
        if (request.RouteId is { } routeId)
        {
            var routeIsValid = await dbContext.AdministrationRoutes
                .AnyAsync(r => r.Id == routeId && r.IsActive, cancellationToken);
            if (!routeIsValid)
                throw new DomainException($"La vía de administración con ID '{routeId}' no existe o está inactiva.");
        }

        var occurredAtUtc = request.OccurredAt.ToUniversalTime();

        var animalEvent = AnimalEvent.CreateForGroup(
            request.GroupId, request.EventType, occurredAtUtc, request.RecordedBy,
            request.PayloadJson, request.AffectedCount, request.Cost, request.RelatedEventId,
            recordedById: request.RecordedById,
            causeId: request.CauseId,
            routeId: request.RouteId,
            reason: request.Reason,
            batchId: request.BatchId,
            healthPlanItemId: request.HealthPlanItemId,
            appliedByUserId: request.AppliedByUserId);

        dbContext.AnimalEvents.Add(animalEvent);

        if (request.EventType == EventType.Disposal)
        {
            await ApplyDisposalAsync(group, request.AffectedCount!.Value, occurredAtUtc, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return animalEvent.Id;
    }

    /// <summary>
    /// A partial disposal only lowers <c>LiveHeadCount</c> — no membership closes, because
    /// choosing which animals left would be exactly the invented data ADR-0015 exists to
    /// avoid. Reaching zero is the one moment the lot can close in bulk without inventing
    /// anything (sec.7): "all of what's left", not "these particular ones".
    /// </summary>
    private async Task ApplyDisposalAsync(
        AnimalGroup group, int affectedCount, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var priorDisposed = await dbContext.AnimalEvents
            .Where(e => e.GroupId == group.Id && e.EventType == EventType.Disposal)
            .SumAsync(e => e.AffectedCount ?? 0, cancellationToken);

        var activeMemberships = group.Memberships.Count(m => m.IsActive);
        var liveHeadCountBefore = activeMemberships - priorDisposed;

        if (affectedCount > liveHeadCountBefore)
        {
            throw new DomainException(
                $"No se pueden dar de baja {affectedCount} cabezas: el lote tiene {liveHeadCountBefore} vivas.");
        }

        if (affectedCount == liveHeadCountBefore)
        {
            var closedAt = DateOnly.FromDateTime(occurredAt.UtcDateTime);
            var closedAnimalIds = group.CloseAllActiveMemberships(closedAt);

            var animals = await dbContext.Animals
                .Where(a => closedAnimalIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

            foreach (var animal in animals)
            {
                animal.CloseViaLotDisposal(occurredAt);
            }
        }
    }
}