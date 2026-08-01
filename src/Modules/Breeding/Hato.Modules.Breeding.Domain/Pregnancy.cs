using Hato.SharedKernel;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;

namespace Hato.Modules.Breeding.Domain;

public class Pregnancy : AuditableEntity
{
    public Guid DamId { get; private set; }
    public Guid? ServiceId { get; private set; }
    public DateOnly ConfirmedAt { get; private set; }
    public DateOnly ExpectedBirthDate { get; private set; }
    public PregnancyStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private Pregnancy() { } // EF Core

    public static Pregnancy Start(
        Guid damId,
        Guid? serviceId,
        DateOnly serviceDate,
        int speciesGestationDays,
        DateOnly confirmedAt,
        string? notes = null)
    {
        if (damId == Guid.Empty)
            throw new DomainException("Dam ID is required to start a pregnancy.");
        if (speciesGestationDays <= 0)
            throw new DomainException("Gestation days parameter must be positive.");

        var expectedBirthDate = serviceDate.AddDays(speciesGestationDays);

        var pregnancy = new Pregnancy
        {
            Id = Guid.NewGuid(),
            DamId = damId,
            ServiceId = serviceId,
            ConfirmedAt = confirmedAt,
            ExpectedBirthDate = expectedBirthDate,
            Status = PregnancyStatus.Active,
            Notes = notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        pregnancy.RaiseDomainEvent(new PregnancyConfirmedEvent(
            pregnancy.Id,
            pregnancy.DamId,
            pregnancy.ServiceId,
            pregnancy.ExpectedBirthDate
        ));

        return pregnancy;
    }

    public void MarkAborted(DateOnly abortDate, string reason)
    {
        if (Status != PregnancyStatus.Active)
            throw new DomainException($"Cannot mark pregnancy as aborted when status is {Status}.");

        Status = PregnancyStatus.Aborted;
        Notes = string.IsNullOrWhiteSpace(Notes) ? $"Aborted on {abortDate}: {reason}" : $"{Notes} | Aborted on {abortDate}: {reason}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted()
    {
        if (Status != PregnancyStatus.Active)
            throw new DomainException($"Cannot complete pregnancy when status is {Status}.");

        Status = PregnancyStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
