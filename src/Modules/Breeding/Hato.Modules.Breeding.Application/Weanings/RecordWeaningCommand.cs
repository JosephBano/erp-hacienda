using FluentValidation;
using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Weanings;

public record RecordWeaningCommand(Guid BirthingId, DateOnly WeaningDate, int WeanedCount, string? Notes = null)
    : IRequest<BirthingDto>;

public class RecordWeaningCommandValidator : AbstractValidator<RecordWeaningCommand>
{
    public RecordWeaningCommandValidator()
    {
        RuleFor(x => x.BirthingId).NotEmpty();
        RuleFor(x => x.WeaningDate).NotEmpty();
        RuleFor(x => x.WeanedCount).GreaterThanOrEqualTo(0);
    }
}

public class RecordWeaningCommandHandler(IBreedingDbContext dbContext)
    : IRequestHandler<RecordWeaningCommand, BirthingDto>
{
    public async Task<BirthingDto> Handle(RecordWeaningCommand request, CancellationToken cancellationToken)
    {
        var birthing = await dbContext.Birthings.FirstOrDefaultAsync(b => b.Id == request.BirthingId, cancellationToken)
            ?? throw new DomainException($"El parto con ID '{request.BirthingId}' no existe.");

        birthing.RecordWeaning(request.WeaningDate, request.WeanedCount, request.Notes);
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
            birthing.CreatedAt,
            birthing.WeanedAt,
            birthing.WeanedCount
        );
    }
}
