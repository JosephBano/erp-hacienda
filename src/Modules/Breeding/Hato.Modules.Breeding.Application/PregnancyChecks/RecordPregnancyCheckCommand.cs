using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Livestock.Contracts;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.PregnancyChecks;

public record RecordPregnancyCheckCommand(
    Guid ServiceId,
    DateOnly CheckDate,
    CheckMethod Method,
    CheckResult Result,
    // Null = resolve from the dam's Species.GestationDays (Art. 8: gestation length is
    // DB configuration per species, never a hardcoded constant). Only set explicitly to
    // override a specific check.
    int? GestationDays = null,
    string? CheckedBy = null,
    string? Notes = null
) : IRequest<PregnancyCheckDto>;

public class RecordPregnancyCheckCommandValidator : AbstractValidator<RecordPregnancyCheckCommand>
{
    public RecordPregnancyCheckCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CheckDate).NotEmpty();
        RuleFor(x => x.GestationDays).GreaterThan(0).When(x => x.GestationDays.HasValue);
    }
}

public class RecordPregnancyCheckCommandHandler(IBreedingDbContext dbContext, IAnimalSpeciesReader speciesReader)
    : IRequestHandler<RecordPregnancyCheckCommand, PregnancyCheckDto>
{
    public async Task<PregnancyCheckDto> Handle(RecordPregnancyCheckCommand request, CancellationToken cancellationToken)
    {
        var service = await dbContext.BreedingServices.FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken)
            ?? throw new InvalidOperationException($"Breeding service with ID {request.ServiceId} not found.");

        var check = PregnancyCheck.Create(
            request.ServiceId,
            service.DamId,
            request.CheckDate,
            request.Method,
            request.Result,
            request.CheckedBy,
            request.Notes
        );

        dbContext.PregnancyChecks.Add(check);

        if (request.Result == CheckResult.Positive)
        {
            var gestationDays = request.GestationDays
                ?? await speciesReader.GetGestationDaysAsync(service.DamId, cancellationToken)
                ?? throw new DomainException(
                    "La especie de la madre no tiene configurados los días de gestación. Configure 'GestationDays' en Especies antes de registrar la preñez.");

            var pregnancy = Pregnancy.Start(
                service.DamId,
                service.Id,
                service.ServiceDate,
                gestationDays,
                request.CheckDate,
                request.Notes
            );

            dbContext.Pregnancies.Add(pregnancy);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PregnancyCheckDto(
            check.Id,
            check.ServiceId,
            check.DamId,
            check.CheckDate,
            check.Method.ToString(),
            check.Result.ToString(),
            check.CheckedBy,
            check.Notes,
            check.CreatedAt
        );
    }
}
