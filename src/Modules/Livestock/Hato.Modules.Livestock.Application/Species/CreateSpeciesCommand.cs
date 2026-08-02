using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;

namespace Hato.Modules.Livestock.Application.Species;

public record CreateSpeciesCommand(string Name, int? GestationDays) : IRequest<Guid>;

public class CreateSpeciesHandler(ILivestockDbContext dbContext) : IRequestHandler<CreateSpeciesCommand, Guid>
{
    public async Task<Guid> Handle(CreateSpeciesCommand request, CancellationToken cancellationToken)
    {
        var species = Hato.Modules.Livestock.Domain.Species.Create(request.Name, request.GestationDays);

        dbContext.Species.Add(species);
        await dbContext.SaveChangesAsync(cancellationToken);

        return species.Id;
    }
}
