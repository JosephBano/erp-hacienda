using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

public class MilkingEligibilityReader(LivestockDbContext dbContext) : IMilkingEligibilityReader
{
    public async Task<AnimalMilkingEligibilityDto?> GetEligibilityAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var batch = await GetEligibilityBatchAsync([animalId], cancellationToken);
        return batch.TryGetValue(animalId, out var dto) ? dto : null;
    }

    public async Task<IReadOnlyDictionary<Guid, AnimalMilkingEligibilityDto>> GetEligibilityBatchAsync(
        IReadOnlyCollection<Guid> animalIds, CancellationToken cancellationToken)
    {
        if (animalIds.Count == 0)
            return new Dictionary<Guid, AnimalMilkingEligibilityDto>();

        var distinctIds = animalIds.Distinct().ToList();

        var animals = await (
            from a in dbContext.Animals.AsNoTracking()
            where distinctIds.Contains(a.Id)
            join s in dbContext.Species.AsNoTracking() on a.SpeciesId equals s.Id into speciesJoin
            from sp in speciesJoin.DefaultIfEmpty()
            select new
            {
                AnimalId = a.Id,
                IsFemale = a.Sex == Sex.Female,
                IsSpeciesMilkable = sp != null && sp.IsMilkable,
                SpeciesName = sp != null ? sp.Name : null,
                DisposedAt = a.DisposedAt
            }
        ).ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, AnimalMilkingEligibilityDto>();
        foreach (var item in animals)
        {
            result[item.AnimalId] = new AnimalMilkingEligibilityDto(
                item.AnimalId,
                Exists: true,
                IsFemale: item.IsFemale,
                IsSpeciesMilkable: item.IsSpeciesMilkable,
                DisposedAt: item.DisposedAt,
                SpeciesName: item.SpeciesName);
        }

        return result;
    }
}
