using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;

namespace Hato.Modules.Livestock.Application.Species;

public record CreateSpeciesCommand(
    string Name,
    int? GestationDays,
    bool IsMilkable = false,
    int? DaysOfLactation = null,
    int? CohortWindowDays = null) : IRequest<Guid>;

public class CreateSpeciesHandler(ILivestockDbContext dbContext) : IRequestHandler<CreateSpeciesCommand, Guid>
{
    public async Task<Guid> Handle(CreateSpeciesCommand request, CancellationToken cancellationToken)
    {
        // Art. 8: species-level capability lives in the database. The flag defaults to
        // false (fail-closed) so a forgotten species never silently allows milk recording.
        var species = Hato.Modules.Livestock.Domain.Species.Create(
            request.Name,
            request.GestationDays,
            request.IsMilkable,
            request.DaysOfLactation,
            request.CohortWindowDays);

        dbContext.Species.Add(species);
        await dbContext.SaveChangesAsync(cancellationToken);

        return species.Id;
    }
}
