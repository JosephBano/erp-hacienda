using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

/// <summary>
/// <paramref name="Id"/> is optional and exists for the offline client (Art. 3: the
/// internal UUID is sovereign and generatable on the phone). A device that registers an
/// animal in the paddock needs to reference it immediately — in a weighing, a treatment,
/// a group move — while still offline, which is impossible if the id only comes into
/// existence when the server answers.
/// </summary>
public record RegisterAnimalCommand(
    Guid SpeciesId,
    Sex Sex,
    DateOnly? BirthDate,
    Guid? BreedId,
    Guid? CategoryId,
    Guid? Id = null) : IRequest<Guid>;

public class RegisterAnimalHandler(ILivestockDbContext dbContext) : IRequestHandler<RegisterAnimalCommand, Guid>
{
    public async Task<Guid> Handle(RegisterAnimalCommand request, CancellationToken cancellationToken)
    {
        // A client-chosen id that already exists means the same registration arrived
        // twice by a route other than the sync operation log — a reinstalled app
        // replaying its outbox, say. Returning the existing animal keeps the operation
        // idempotent instead of failing on the primary key.
        if (request.Id is { } requestedId && requestedId != Guid.Empty)
        {
            var existing = await dbContext.Animals
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == requestedId, cancellationToken);

            if (existing is not null)
            {
                return existing.Id;
            }
        }

        var animal = Animal.Register(
            request.SpeciesId, request.Sex, request.BirthDate, request.BreedId, request.CategoryId,
            birthWeightKg: null,
            id: request.Id);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return animal.Id;
    }
}
