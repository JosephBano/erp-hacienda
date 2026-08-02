namespace Hato.Modules.Livestock.Contracts;

public enum WithdrawalTargetKind
{
    Milk,
    Meat,
    Both
}

public record ActiveWithdrawalDto(Guid Id, Guid AnimalId, WithdrawalTargetKind Target, DateOnly StartsAt, DateOnly EndsAt);

/// <summary>
/// Public read port into Livestock's withdrawal periods. Other modules (Tasks, Production)
/// depend only on this contract — never on Livestock.Domain/Infrastructure (Art. 6).
/// </summary>
public interface IWithdrawalPeriodsReader
{
    Task<IReadOnlyList<ActiveWithdrawalDto>> GetActiveAsOfAsync(DateOnly date, CancellationToken cancellationToken);

    Task<bool> HasActiveWithdrawalAsync(Guid animalId, DateOnly date, WithdrawalTargetKind target, CancellationToken cancellationToken);
}
