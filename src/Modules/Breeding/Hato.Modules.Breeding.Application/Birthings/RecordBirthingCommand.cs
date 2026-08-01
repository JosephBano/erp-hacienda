using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;
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

public class RecordBirthingCommandHandler(IBreedingDbContext dbContext)
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
