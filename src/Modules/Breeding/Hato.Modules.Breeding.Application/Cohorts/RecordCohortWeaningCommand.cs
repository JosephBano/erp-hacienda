using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Livestock.Contracts;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Cohorts;

/// <summary>
/// Weans a nursing cohort as a whole (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.4, task 2).
///
/// The teaching date is computed, not declared: <c>weaning_date = max(BirthDate of its
/// Birthings) + DaysOfLactation</c>. The handler then walks the cohort's birthings and
/// records the per-birthing weaning (Art. 1: each birthing is its own event, and the
/// cohort is the aggregate that ties them together for the calendar). The operation is
/// idempotent at the cohort level: a cohort that has already been weaned cannot be
/// weaned again, and the error is loud rather than silent.
/// </summary>
public record RecordCohortWeaningCommand(
    Guid NursingCohortId,
    DateOnly? WeaningDate = null,
    string? Notes = null) : IRequest<NursingCohortDto>;

public class RecordCohortWeaningCommandValidator : AbstractValidator<RecordCohortWeaningCommand>
{
    public RecordCohortWeaningCommandValidator()
    {
        RuleFor(x => x.NursingCohortId).NotEmpty();
    }
}

public class RecordCohortWeaningCommandHandler(
    IBreedingDbContext dbContext,
    IAnimalSpeciesReader speciesReader,
    IAnimalRegistrationService animalRegistration) : IRequestHandler<RecordCohortWeaningCommand, NursingCohortDto>
{
    public async Task<NursingCohortDto> Handle(
        RecordCohortWeaningCommand request,
        CancellationToken cancellationToken)
    {
        var cohort = await dbContext.NursingCohorts
            .FirstOrDefaultAsync(c => c.Id == request.NursingCohortId, cancellationToken)
            ?? throw new DomainException($"La cohorte '{request.NursingCohortId}' no existe.");

        var birthings = await dbContext.Birthings
            .Where(b => b.NursingCohortId == cohort.Id)
            .OrderBy(b => b.BirthDate)
            .ToListAsync(cancellationToken);

        if (birthings.Count == 0)
        {
            throw new DomainException(
                "La cohorte no tiene camadas registradas. No se puede destetar una cohorte vacía.");
        }

        var profile = await speciesReader.GetLactationProfileAsync(cohort.SpeciesId, cancellationToken)
            ?? throw new DomainException(
                "La especie de la cohorte ya no existe; no se puede calcular el destete.");

        if (profile.DaysOfLactation is null)
        {
            throw new DomainException(
                "La especie no tiene días de lactancia configurados. Configure el parámetro en el panel antes de destetar.");
        }

        var latestBirth = birthings[^1].BirthDate;
        var computedWeaningDate = cohort.ComputeWeaningDate(profile.DaysOfLactation.Value, latestBirth);

        // The operator may override the computed date (the field wins when the two
        // disagree by a day or two), but the override cannot predate the cohort's start.
        var weaningDate = request.WeaningDate ?? computedWeaningDate;
        if (weaningDate < cohort.StartedAt)
        {
            throw new DomainException(
                "La fecha de destete no puede ser anterior al inicio de la cohorte.");
        }

        // Per-birthing weaning events. Each event still validates
        // weanedCount <= BornAlive (the existing Birthing.RecordWeaning invariant).
        // The cohort carries the date; the per-birthing record carries the count.
        //
        // The weaned count is NOT BornAlive: that would silently inflate every weaning
        // by the preweaning deaths (3.5a.3's whole reason for existing). We compute it
        // from the AnimalEvents of this litter — animals whose BirthingId is this one
        // and that already carry a Disposal event.
        var totalWeaned = 0;
        foreach (var birthing in birthings)
        {
            if (birthing.WeanedAt is not null)
            {
                continue; // already weaned (partial correction flow), skip silently
            }

            var preweaningDeaths = await animalRegistration.CountPreweaningDeathsAsync(birthing.Id, cancellationToken);
            var weanedCount = Math.Max(0, birthing.BornAlive - preweaningDeaths);

            birthing.RecordWeaning(weaningDate, weanedCount, notes: null);
            totalWeaned += weanedCount;
        }

        cohort.RecordWeaning(weaningDate, totalWeaned, request.Notes);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NursingCohortDto(
            cohort.Id,
            cohort.SpeciesId,
            cohort.StartedAt,
            cohort.ClosedAt,
            cohort.WeanedAt,
            birthings.Count,
            totalWeaned,
            cohort.Notes,
            cohort.CreatedAt);
    }
}
