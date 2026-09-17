using FluentValidation;
using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Application.Milking;

public record IndividualYieldItem(Guid AnimalId, decimal Liters);

public record RecordMilkingSessionCommand(
    DateOnly Date,
    MilkingShift Shift,
    string RecordedBy,
    decimal TotalLiters,
    Guid? GroupId = null,
    string? Notes = null,
    List<IndividualYieldItem>? IndividualYields = null,
    bool IsPlausibilityConfirmed = false) : IRequest<Guid>;

public class RecordMilkingSessionValidator : AbstractValidator<RecordMilkingSessionCommand>
{
    public RecordMilkingSessionValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TotalLiters).GreaterThanOrEqualTo(0);
    }
}

public class RecordMilkingSessionHandler(
    IProductionDbContext dbContext,
    IWithdrawalPeriodsReader withdrawals,
    IMilkingEligibilityReader milkingEligibility)
    : IRequestHandler<RecordMilkingSessionCommand, Guid>
{
    public async Task<Guid> Handle(RecordMilkingSessionCommand request, CancellationToken cancellationToken)
    {
        // Art. 19: a group/tank session pools milk from every member of the group, so one
        // withheld animal is enough to withhold the whole session — not just the animals
        // listed individually below.
        if (request.GroupId is Guid groupId)
        {
            var withheldInGroup = await withdrawals.GetWithdrawnAnimalIdsInGroupAsync(
                groupId, request.Date, WithdrawalTargetKind.Milk, cancellationToken);

            if (withheldInGroup.Count > 0)
                throw new DomainException(
                    $"El grupo tiene animal(es) con período de retiro de leche activo y no puede registrarse el ordeño grupal: {string.Join(", ", withheldInGroup)}.");
        }

        if (request.IndividualYields is not null && request.IndividualYields.Count > 0)
        {
            var distinctAnimalIds = request.IndividualYields.Select(y => y.AnimalId).Distinct().ToList();
            var eligibilityMap = await milkingEligibility.GetEligibilityBatchAsync(distinctAnimalIds, cancellationToken);

            foreach (var item in request.IndividualYields)
            {
                if (!eligibilityMap.TryGetValue(item.AnimalId, out var eligibility) || !eligibility.Exists)
                {
                    throw new DomainException($"El animal con ID '{item.AnimalId}' no existe.");
                }

                if (!eligibility.IsFemale)
                {
                    throw new DomainException("Solo se pueden ordeñar animales de sexo hembra.");
                }

                if (eligibility.DisposedAt is { } disposedAt)
                {
                    var disposedDate = DateOnly.FromDateTime(disposedAt.UtcDateTime);
                    if (disposedDate <= request.Date)
                    {
                        throw new DomainException("El animal fue dado de baja y no puede ser ordeñado.");
                    }
                }

                if (!eligibility.IsSpeciesMilkable)
                {
                    throw new DomainException(
                        string.IsNullOrWhiteSpace(eligibility.SpeciesName)
                            ? "La especie no es ordeñable."
                            : $"La especie '{eligibility.SpeciesName}' no está habilitada para ordeño.");
                }

                // Art. 19: milk from an animal under an active withdrawal cannot be sold —
                // enforced here at the point of entry, not left to a report someone reads later.
                var isWithheld = await withdrawals.HasActiveWithdrawalAsync(
                    item.AnimalId, request.Date, WithdrawalTargetKind.Milk, cancellationToken);

                if (isWithheld)
                    throw new DomainException(
                        $"El animal '{item.AnimalId}' tiene un período de retiro de leche activo y no puede registrarse su producción.");
            }
        }

        var session = MilkingSession.Create(
            request.Date,
            request.Shift,
            request.RecordedBy,
            request.TotalLiters,
            request.GroupId,
            request.Notes,
            isPlausibilityConfirmed: request.IsPlausibilityConfirmed);

        if (request.IndividualYields is not null && request.IndividualYields.Count > 0)
        {
            foreach (var item in request.IndividualYields)
            {
                var y = session.RecordAnimalYield(item.AnimalId, item.Liters);
                dbContext.MilkYields.Add(y);
            }
        }

        dbContext.MilkingSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
