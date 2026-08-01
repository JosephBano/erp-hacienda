using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
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
    List<OffspringBirthInfo>? Offspring = null
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
    }
}

public class RecordBirthingCommandHandler(IBreedingDbContext dbContext, IAnimalRegistrationService animalRegistration)
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

        // Try resolving sire / straw from the pregnancy -> service
        Guid? sireAnimalId = null;
        Guid? fatherStrawId = null;
        if (pregnancy?.ServiceId is not null)
        {
            var service = await dbContext.BreedingServices.FirstOrDefaultAsync(s => s.Id == pregnancy.ServiceId.Value, cancellationToken);
            if (service is not null)
            {
                sireAnimalId = service.SireAnimalId;
                fatherStrawId = service.StrawId;
            }
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
            request.Offspring
        );

        dbContext.Birthings.Add(birthing);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Enroll each live-born calf as a first-class Animal in Livestock, with
        // genealogy set — this is what makes "the calf was born inside the system"
        // (Fase 2 exit criterion) true, instead of just recording a litter count.
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
                        BirthingId: birthing.Id),
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
            birthing.CreatedAt
        );
    }
}
