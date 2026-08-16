using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Species;

/// <summary>
/// docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.4 task: the lactation parameters (DaysOfLactation,
/// CohortWindowDays) used to be exposed on the entity but had no command, no endpoint and
/// no caller. RecordCohortWeaningCommand reached for them through IAnimalSpeciesReader and
/// refused with "Configure el parámetro en el panel" — except the panel never had a way
/// to set them. This command closes the loop: an operator-facing PATCH that lets the
/// admin-web panel (or a curl) flip the values without a deploy (Art. 8).
/// </summary>
public record UpdateSpeciesLactationCommand(
    Guid SpeciesId,
    int? DaysOfLactation,
    int? CohortWindowDays) : IRequest<Unit>;

public class UpdateSpeciesLactationValidator : AbstractValidator<UpdateSpeciesLactationCommand>
{
    public UpdateSpeciesLactationValidator()
    {
        RuleFor(x => x.SpeciesId).NotEmpty();
        RuleFor(x => x.DaysOfLactation).GreaterThan(0).When(x => x.DaysOfLactation.HasValue);
        RuleFor(x => x.CohortWindowDays).GreaterThan(0).When(x => x.CohortWindowDays.HasValue);
    }
}

public class UpdateSpeciesLactationHandler(ILivestockDbContext dbContext)
    : IRequestHandler<UpdateSpeciesLactationCommand, Unit>
{
    public async Task<Unit> Handle(
        UpdateSpeciesLactationCommand request,
        CancellationToken cancellationToken)
    {
        var species = await dbContext.Species
            .FirstOrDefaultAsync(s => s.Id == request.SpeciesId, cancellationToken)
            ?? throw new DomainException($"La especie con ID '{request.SpeciesId}' no existe.");

        species.UpdateLactation(request.DaysOfLactation, request.CohortWindowDays);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}