using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Services;

public record RegisterBreedingServiceCommand(
    Guid DamId,
    ServiceType ServiceType,
    DateOnly ServiceDate,
    Guid? SireAnimalId = null,
    Guid? StrawId = null,
    string? Technician = null,
    string? Notes = null,
    decimal? BodyConditionScore = null
) : IRequest<BreedingServiceDto>;

public class RegisterBreedingServiceCommandValidator : AbstractValidator<RegisterBreedingServiceCommand>
{
    public RegisterBreedingServiceCommandValidator()
    {
        RuleFor(x => x.DamId).NotEmpty();
        RuleFor(x => x.ServiceDate).NotEmpty();
        RuleFor(x => x).Must(x => (x.SireAnimalId.HasValue && !x.StrawId.HasValue) || (!x.SireAnimalId.HasValue && x.StrawId.HasValue))
            .WithMessage("Must specify either SireAnimalId or StrawId, but not both.");
    }
}

public class RegisterBreedingServiceCommandHandler(IBreedingDbContext dbContext)
    : IRequestHandler<RegisterBreedingServiceCommand, BreedingServiceDto>
{
    public async Task<BreedingServiceDto> Handle(RegisterBreedingServiceCommand request, CancellationToken cancellationToken)
    {
        if (request.StrawId.HasValue)
        {
            var straw = await dbContext.SemenStraws.FirstOrDefaultAsync(s => s.Id == request.StrawId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Semen straw with ID {request.StrawId.Value} not found.");

            straw.UseStraw();
        }

        var service = BreedingService.Create(
            request.DamId,
            request.ServiceType,
            request.ServiceDate,
            request.SireAnimalId,
            request.StrawId,
            request.Technician,
            request.Notes,
            request.BodyConditionScore
        );

        dbContext.BreedingServices.Add(service);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BreedingServiceDto(
            service.Id,
            service.DamId,
            service.ServiceType.ToString(),
            service.SireAnimalId,
            service.StrawId,
            service.ServiceDate,
            service.Technician,
            service.Notes,
            service.BodyConditionScore,
            service.CreatedAt
        );
    }
}
