using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Application.CrossModule;

public class MilkYieldsReader(IProductionDbContext dbContext) : IMilkYieldsReader
{
    public async Task<IReadOnlyList<AnimalMilkYieldDto>> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var yields = await dbContext.MilkYields
            .AsNoTracking()
            .Where(y => y.AnimalId == animalId)
            .Join(dbContext.MilkingSessions.AsNoTracking(),
                y => y.MilkingSessionId,
                s => s.Id,
                (y, s) => new { y.Id, s.Date, s.Shift, y.Liters })
            .ToListAsync(cancellationToken);

        return yields
            .OrderByDescending(y => y.Date)
            .Select(y => new AnimalMilkYieldDto(y.Id, y.Date, y.Shift.ToString(), y.Liters))
            .ToList();
    }
}
