using Hato.Modules.Production.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Application.Abstractions;

public interface IProductionDbContext
{
    DbSet<MilkingSession> MilkingSessions { get; }
    DbSet<MilkYield> MilkYields { get; }
    DbSet<Lactation> Lactations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
