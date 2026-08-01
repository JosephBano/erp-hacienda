using Microsoft.EntityFrameworkCore;
using Domain = Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.Application.Abstractions;

/// <summary>
/// Persistence port for the Livestock module's Application layer (Clean Architecture:
/// Application depends on this abstraction, never on Infrastructure directly —
/// Infrastructure already depends on Application, so the reverse would be circular).
/// </summary>
public interface ILivestockDbContext
{
    // Fully qualified via the Domain alias: an Application.Species feature namespace
    // exists (CreateSpeciesCommand.cs) that would otherwise shadow the Domain.Species type.
    DbSet<Domain.Species> Species { get; }
    DbSet<Domain.Breed> Breeds { get; }
    DbSet<Domain.AnimalCategory> AnimalCategories { get; }
    DbSet<Domain.Animal> Animals { get; }
    DbSet<Domain.AnimalIdentifier> AnimalIdentifiers { get; }
    DbSet<Domain.AnimalGroup> AnimalGroups { get; }
    DbSet<Domain.GroupMembership> GroupMemberships { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
