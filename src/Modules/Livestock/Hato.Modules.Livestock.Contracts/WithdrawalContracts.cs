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

    /// <summary>
    /// Among the animals currently active in <paramref name="groupId"/>, returns the ones
    /// under an active withdrawal on <paramref name="date"/> for <paramref name="target"/>.
    /// Used to block group/tank milking sessions the same way individual sessions are
    /// blocked (Art. 19) — a group total pools milk from every member, so one withheld
    /// animal in the group is enough to withhold the whole session.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetWithdrawnAnimalIdsInGroupAsync(Guid groupId, DateOnly date, WithdrawalTargetKind target, CancellationToken cancellationToken);
}
