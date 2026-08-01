using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.PregnancyChecks;

public record RecordPregnancyCheckCommand(
    Guid ServiceId,
    DateOnly CheckDate,
    CheckMethod Method,
    CheckResult Result,
    int GestationDays = 283, // Parameterized gestation days by species
    string? CheckedBy = null,
    string? Notes = null
) : IRequest<PregnancyCheckDto>;

public class RecordPregnancyCheckCommandValidator : AbstractValidator<RecordPregnancyCheckCommand>
{
    public RecordPregnancyCheckCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CheckDate).NotEmpty();
        RuleFor(x => x.GestationDays).GreaterThan(0);
    }
}

public class RecordPregnancyCheckCommandHandler(IBreedingDbContext dbContext)
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
            var pregnancy = Pregnancy.Start(
                service.DamId,
                service.Id,
                service.ServiceDate,
                request.GestationDays,
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
