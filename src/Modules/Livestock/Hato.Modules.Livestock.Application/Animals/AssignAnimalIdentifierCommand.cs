using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

public record AssignAnimalIdentifierCommand(
    Guid AnimalId, IdentifierType Type, string Value, DateOnly ValidFrom) : IRequest<Guid>;

public class AssignAnimalIdentifierHandler(ILivestockDbContext dbContext)
    : IRequestHandler<AssignAnimalIdentifierCommand, Guid>
{
    public async Task<Guid> Handle(AssignAnimalIdentifierCommand request, CancellationToken cancellationToken)
    {
        // Identifiers must be loaded explicitly: without it, AssignIdentifier cannot see
        // the currently active one and would try to create a duplicate (Domain invariant,
        // not just the DB's unique index, needs the full aggregate in memory).
        var animal = await dbContext.Animals
            .Include(a => a.Identifiers)
            .FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe el animal {request.AnimalId}.");

        var identifier = animal.AssignIdentifier(request.Type, request.Value, request.ValidFrom);

        // Without this, EF Core only discovers the new identifier via graph traversal
        // during DetectChanges. Since its Id already has a non-default value (client-
        // generated in the domain constructor), EF's heuristic assumes it already exists
        // in the database and emits an UPDATE instead of an INSERT — which matches zero
        // rows and throws DbUpdateConcurrencyException. Adding it explicitly forces
        // EntityState.Added regardless of the key value.
        dbContext.AnimalIdentifiers.Add(identifier);

        await dbContext.SaveChangesAsync(cancellationToken);

        return identifier.Id;
    }
}
