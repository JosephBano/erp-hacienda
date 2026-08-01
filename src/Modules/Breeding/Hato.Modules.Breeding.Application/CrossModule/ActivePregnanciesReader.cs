using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.CrossModule;

public class ActivePregnanciesReader(IBreedingDbContext dbContext) : IActivePregnanciesReader
{
    public async Task<IReadOnlyList<UpcomingBirthDto>> GetUpcomingBirthsAsync(DateOnly thresholdDate, CancellationToken cancellationToken)
    {
        return await dbContext.Pregnancies
            .AsNoTracking()
            .Where(p => p.Status == PregnancyStatus.Active && p.ExpectedBirthDate <= thresholdDate)
            .Select(p => new UpcomingBirthDto(p.Id, p.DamId, p.ExpectedBirthDate))
            .ToListAsync(cancellationToken);
    }
}

public class PendingPregnancyChecksReader(IBreedingDbContext dbContext) : IPendingPregnancyChecksReader
{
    public async Task<IReadOnlyList<PendingPregnancyCheckDto>> GetServicesPendingCheckAsync(DateOnly serviceDateOlderThan, CancellationToken cancellationToken)
    {
        var checkedServiceIds = dbContext.PregnancyChecks.Select(c => c.ServiceId);

        return await dbContext.BreedingServices
            .AsNoTracking()
            .Where(s => s.ServiceDate <= serviceDateOlderThan && !checkedServiceIds.Contains(s.Id))
            .Select(s => new PendingPregnancyCheckDto(s.Id, s.DamId, s.ServiceDate))
            .ToListAsync(cancellationToken);
    }
}
