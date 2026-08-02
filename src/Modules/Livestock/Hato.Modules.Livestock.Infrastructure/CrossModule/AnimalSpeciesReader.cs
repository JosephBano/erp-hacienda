using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

public class AnimalSpeciesReader(LivestockDbContext dbContext) : IAnimalSpeciesReader
{
    public async Task<int?> GetGestationDaysAsync(Guid animalId, CancellationToken cancellationToken)
    {
        return await dbContext.Animals
            .AsNoTracking()
            .Where(a => a.Id == animalId)
            .Join(dbContext.Species.AsNoTracking(), a => a.SpeciesId, s => s.Id, (a, s) => s.GestationDays)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
