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
    Guid? CauseId = null) : IRequest<Guid>;

public class RecordAnimalEventValidator : AbstractValidator<RecordAnimalEventCommand>
{
    public RecordAnimalEventValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PayloadJson).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
    }
}

public class RecordAnimalEventHandler(ILivestockDbContext dbContext)
    : IRequestHandler<RecordAnimalEventCommand, Guid>
{
    public async Task<Guid> Handle(RecordAnimalEventCommand request, CancellationToken cancellationToken)
    {
        var animalExists = await dbContext.Animals.AnyAsync(a => a.Id == request.AnimalId, cancellationToken);
        if (!animalExists)
            throw new DomainException($"El animal con ID '{request.AnimalId}' no existe.");

        if (request.CauseId is { } causeId)
        {
            var causeIsValid = await dbContext.MortalityCauses
                .AnyAsync(c => c.Id == causeId && c.IsActive, cancellationToken);
            if (!causeIsValid)
                throw new DomainException($"La causa de mortalidad con ID '{causeId}' no existe o está inactiva.");
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
            causeId: request.CauseId);

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
