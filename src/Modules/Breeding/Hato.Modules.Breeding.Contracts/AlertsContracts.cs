namespace Hato.Modules.Breeding.Contracts;

public record UpcomingBirthDto(Guid PregnancyId, Guid DamId, DateOnly ExpectedBirthDate);

/// <summary>Public read port used by Tasks/Alerts (Art. 6) instead of raw SQL against breeding.pregnancies.</summary>
public interface IActivePregnanciesReader
{
    Task<IReadOnlyList<UpcomingBirthDto>> GetUpcomingBirthsAsync(DateOnly thresholdDate, CancellationToken cancellationToken);
}

public record PendingPregnancyCheckDto(Guid ServiceId, Guid DamId, DateOnly ServiceDate);

/// <summary>Services registered without a follow-up pregnancy check after the given cutoff.</summary>
public interface IPendingPregnancyChecksReader
{
    Task<IReadOnlyList<PendingPregnancyCheckDto>> GetServicesPendingCheckAsync(DateOnly serviceDateOlderThan, CancellationToken cancellationToken);
}
