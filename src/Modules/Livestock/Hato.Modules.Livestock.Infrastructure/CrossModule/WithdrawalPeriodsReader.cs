using Hato.Modules.Livestock.Contracts;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.CrossModule;

public class WithdrawalPeriodsReader(LivestockDbContext dbContext) : IWithdrawalPeriodsReader
{
    public async Task<IReadOnlyList<ActiveWithdrawalDto>> GetActiveAsOfAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var withdrawals = await dbContext.WithdrawalPeriods
            .AsNoTracking()
            .Where(w => w.StartsAt <= date && w.EndsAt >= date)
            .ToListAsync(cancellationToken);

        return withdrawals
            .Select(w => new ActiveWithdrawalDto(w.Id, w.AnimalId, ToContract(w.Target), w.StartsAt, w.EndsAt))
            .ToList();
    }

    public async Task<bool> HasActiveWithdrawalAsync(Guid animalId, DateOnly date, WithdrawalTargetKind target, CancellationToken cancellationToken)
    {
        var domainTargets = ToDomainTargets(target);

        return await dbContext.WithdrawalPeriods
            .AsNoTracking()
            .AnyAsync(w => w.AnimalId == animalId
                && w.StartsAt <= date && w.EndsAt >= date
                && domainTargets.Contains(w.Target), cancellationToken);
    }

    private static WithdrawalTargetKind ToContract(WithdrawalTarget target) => target switch
    {
        WithdrawalTarget.Milk => WithdrawalTargetKind.Milk,
        WithdrawalTarget.Meat => WithdrawalTargetKind.Meat,
        WithdrawalTarget.Both => WithdrawalTargetKind.Both,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    /// <summary>A withdrawal targeting "Both" also counts for a query about either single target.</summary>
    private static WithdrawalTarget[] ToDomainTargets(WithdrawalTargetKind target) => target switch
    {
        WithdrawalTargetKind.Milk => [WithdrawalTarget.Milk, WithdrawalTarget.Both],
        WithdrawalTargetKind.Meat => [WithdrawalTarget.Meat, WithdrawalTarget.Both],
        WithdrawalTargetKind.Both => [WithdrawalTarget.Both],
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
