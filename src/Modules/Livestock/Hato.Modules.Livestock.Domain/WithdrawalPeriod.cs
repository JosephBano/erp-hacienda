using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Active withdrawal period post-treatment (Art. 19 + GLOSSARY.md).
/// Milk or meat from animals under active withdrawal cannot be sold (blocking rule).
/// </summary>
public class WithdrawalPeriod : AuditableEntity
{
    public Guid AnimalId { get; private set; }
    public Guid EventId { get; private set; }
    public WithdrawalTarget Target { get; private set; }
    public DateOnly StartsAt { get; private set; }
    public DateOnly EndsAt { get; private set; }

    private WithdrawalPeriod() { }

    public WithdrawalPeriod(Guid animalId, Guid eventId, WithdrawalTarget target, DateOnly startsAt, DateOnly endsAt)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("El período de retiro debe estar asociado a un animal.");

        if (eventId == Guid.Empty)
            throw new DomainException("El período de retiro debe estar asociado a un evento.");

        if (endsAt < startsAt)
            throw new DomainException("La fecha de fin del retiro no puede ser anterior a la fecha de inicio.");

        AnimalId = animalId;
        EventId = eventId;
        Target = target;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public bool IsActiveOn(DateOnly date) => date >= StartsAt && date <= EndsAt;
}
