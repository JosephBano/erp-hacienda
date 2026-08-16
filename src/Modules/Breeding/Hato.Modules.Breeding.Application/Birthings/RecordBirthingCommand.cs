using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Application.Cohorts;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;
using Hato.Modules.Livestock.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Birthings;

public record RecordBirthingCommand(
    Guid DamId,
    DateOnly BirthDate,
    BirthingDifficulty Difficulty,
    int BornAlive,
    int BornDead = 0,
    int Mummified = 0,
    Guid? PregnancyId = null,
    decimal? LitterWeight = null,
    string? Notes = null,
    List<OffspringBirthInfo>? Offspring = null,
    Guid? SireAnimalId = null,
    Guid? SireStrawId = null
) : IRequest<BirthingDto>;

public class RecordBirthingCommandValidator : AbstractValidator<RecordBirthingCommand>
{
    public RecordBirthingCommandValidator()
    {
        RuleFor(x => x.DamId).NotEmpty();
        RuleFor(x => x.BirthDate).NotEmpty();
        RuleFor(x => x.BornAlive).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BornDead).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Mummified).GreaterThanOrEqualTo(0);
        RuleFor(x => (x.BornAlive + x.BornDead + x.Mummified)).GreaterThan(0).WithMessage("Total born count must be greater than zero.");

        // Dual father (ADR-0006): the sire is an animal *or* a straw, never both.
        RuleFor(x => x)
            .Must(x => !(x.SireAnimalId.HasValue && x.SireStrawId.HasValue))
            .WithMessage("El padre no puede ser un animal y una pajuela al mismo tiempo.");
    }
}

public class RecordBirthingCommandHandler(
    IBreedingDbContext dbContext,
    IAnimalRegistrationService animalRegistration,
    INursingCohortAssigner cohortAssigner)
    : IRequestHandler<RecordBirthingCommand, BirthingDto>
{
    public async Task<BirthingDto> Handle(RecordBirthingCommand request, CancellationToken cancellationToken)
    {
        Pregnancy? pregnancy = null;
        if (request.PregnancyId.HasValue)
        {
            pregnancy = await dbContext.Pregnancies.FirstOrDefaultAsync(p => p.Id == request.PregnancyId.Value, cancellationToken);
        }
        else
        {
            pregnancy = await dbContext.Pregnancies.FirstOrDefaultAsync(
                p => p.DamId == request.DamId && p.Status == PregnancyStatus.Active, cancellationToken);
        }

        if (pregnancy is not null && pregnancy.Status == PregnancyStatus.Active)
        {
            pregnancy.MarkCompleted();
        }

        // The sire declared by whoever witnessed the birth wins: a field-recorded birth
        // frequently has no pregnancy behind it (natural mating, nobody logged a service),
        // and that person is the only source of the parentage there will ever be.
        Guid? sireAnimalId = request.SireAnimalId;
        Guid? fatherStrawId = request.SireStrawId;

        if (sireAnimalId is null && fatherStrawId is null && pregnancy?.ServiceId is not null)
        {
            var service = await dbContext.BreedingServices.FirstOrDefaultAsync(s => s.Id == pregnancy.ServiceId.Value, cancellationToken);
            if (service is not null)
            {
                sireAnimalId = service.SireAnimalId;
                fatherStrawId = service.StrawId;
            }
        }

        // Pull the species off the dam so the cohort assigner knows which window to apply.
        var speciesId = await ResolveDamSpeciesAsync(request.DamId, cancellationToken);

        // Cohort assignment happens before Save so the new birthings row carries the
        // nursing_cohort_id; the assigner opens a new cohort when no candidate is open.
        Guid? cohortId = null;
        if (speciesId.HasValue)
        {
            cohortId = await cohortAssigner.AssignAsync(speciesId.Value, request.BirthDate, cancellationToken);
        }

        var birthing = Birthing.Create(
            request.DamId,
            request.BirthDate,
            request.Difficulty,
            request.BornAlive,
            request.BornDead,
            request.Mummified,
            pregnancy?.Id,
            request.LitterWeight,
            request.Notes,
            sireAnimalId,
            fatherStrawId,
            request.Offspring,
            cohortId
        );

        dbContext.Birthings.Add(birthing);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Enroll each live-born calf as a first-class Animal in Livestock, with
        // genealogy set — this is what makes "the calf was born inside the system"
        // (Fase 2 exit criterion) true, instead of just recording a litter count.
        // BirthWeightKg is propagated from the field-app (docs/planes/fase-3-5/spec.md
        // sec.3.5a.4 task 3): without it, every metric that derives from the
        // first-day weight — gilt selection, pre-weaning growth — is unrecoverable
        // once the litter is mixed into the headcount lot.
        if (request.Offspring is { Count: > 0 })
        {
            foreach (var offspring in request.Offspring)
            {
                await animalRegistration.RegisterOffspringAsync(
                    new RegisterOffspringRequest(
                        DamId: request.DamId,
                        Sex: offspring.Sex,
                        BirthDate: request.BirthDate,
                        CategoryId: offspring.CategoryId,
                        FarmTag: offspring.FarmTag,
                        FatherAnimalId: sireAnimalId,
                        FatherStrawId: fatherStrawId,
                        BirthingId: birthing.Id,
                        BirthWeightKg: offspring.BirthWeightKg),
                    cancellationToken);
            }
        }

        return new BirthingDto(
            birthing.Id,
            birthing.DamId,
            birthing.PregnancyId,
            birthing.BirthDate,
            birthing.Difficulty.ToString(),
            birthing.TotalBorn,
            birthing.BornAlive,
            birthing.BornDead,
            birthing.Mummified,
            birthing.LitterWeight,
            birthing.Notes,
            birthing.CreatedAt,
            birthing.WeanedAt,
            birthing.WeanedCount,
            birthing.NursingCohortId
        );
    }

    /// <summary>
    /// The cohort assigner needs the species id of the dam. The cross-module
    /// <see cref="IAnimalRegistrationService"/> port exposes a read-only species lookup
    /// for exactly this purpose (the dam registration may live in Livestock.Domain
    /// without Breeding importing the table). If the dam was deleted or the species
    /// cannot be resolved, the birth is recorded without grouping — the kinship
    /// through the birthing row is preserved either way.
    /// </summary>
    private async Task<Guid?> ResolveDamSpeciesAsync(Guid damId, CancellationToken cancellationToken)
    {
        return await animalRegistration.GetSpeciesAsync(damId, cancellationToken);
    }
}
