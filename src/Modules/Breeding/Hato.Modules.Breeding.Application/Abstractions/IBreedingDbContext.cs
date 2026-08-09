using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Abstractions;

public interface IBreedingDbContext
{
    DbSet<SemenStraw> SemenStraws { get; }
    DbSet<BreedingService> BreedingServices { get; }
    DbSet<PregnancyCheck> PregnancyChecks { get; }
    DbSet<Pregnancy> Pregnancies { get; }
    DbSet<Birthing> Birthings { get; }
    DbSet<NursingCohort> NursingCohorts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
