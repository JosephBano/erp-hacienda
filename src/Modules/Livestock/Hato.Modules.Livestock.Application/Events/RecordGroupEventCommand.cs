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
    Guid? RelatedEventId = null) : IRequest<Guid>;

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

        var animalEvent = AnimalEvent.CreateForGroup(
            request.GroupId, request.EventType, request.OccurredAt, request.RecordedBy,
            request.PayloadJson, request.AffectedCount, request.Cost, request.RelatedEventId);

        dbContext.AnimalEvents.Add(animalEvent);

        if (request.EventType == EventType.Disposal)
        {
            await ApplyDisposalAsync(group, request.AffectedCount!.Value, request.OccurredAt, cancellationToken);
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
