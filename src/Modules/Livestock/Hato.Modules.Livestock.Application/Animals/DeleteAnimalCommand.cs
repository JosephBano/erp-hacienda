using Hato.Modules.Livestock.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

/// <summary>
/// Undoes a mis-registered animal. A tombstone (Art. 1), not a physical removal — the
/// sync pull keeps delivering it once with <c>isDeleted: true</c> so every device drops
/// it, and the row itself is never touched again after that.
/// </summary>
public record DeleteAnimalCommand(Guid AnimalId) : IRequest;

public class DeleteAnimalHandler(ILivestockDbContext dbContext) : IRequestHandler<DeleteAnimalCommand>
{
    public async Task Handle(DeleteAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await dbContext.Animals.FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"El animal con ID '{request.AnimalId}' no existe.");

        // The aggregate itself cannot see events recorded against it in another module
        // (Art. 6); this is the one invariant that belongs at the Application boundary:
        // an animal with real recorded history is not "a mistake" any more, and deleting
        // it would leave its events pointing at a hidden animal.
        var hasHistory = await dbContext.AnimalEvents
            .AnyAsync(e => e.AnimalId == request.AnimalId, cancellationToken);

        if (hasHistory)
        {
            throw new DomainException(
                "No se puede eliminar un animal con eventos registrados. " +
                "Si el registro fue un error, la corrección es un evento nuevo (Art. 1).");
        }

        animal.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
