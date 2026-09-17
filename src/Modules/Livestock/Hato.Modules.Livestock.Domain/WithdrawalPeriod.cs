using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Active withdrawal period post-treatment (Art. 19 + GLOSSARY.md).
/// Milk or meat from animals under active withdrawal cannot be sold (blocking rule).
/// </summary>
public class WithdrawalPeriod : AuditableEntity
{
    public Guid AnimalId { get; private set; }

    /// <summary>Legacy anchor: the loose <see cref="AnimalEvent"/> that produced this
    /// period, pre-3.5a.2-B. Nullable so a period anchored to a
    /// <see cref="TreatmentCourse"/> (see <see cref="TreatmentCourseId"/>) can leave
    /// it empty — exactly one of the two is set (task 5: the period is computed from
    /// the course's last application, not a loose event).</summary>
    public Guid? EventId { get; private set; }

    /// <summary>New anchor (3.5a.2-B task 5): the <see cref="TreatmentCourse"/> whose
    /// last application produced this period. Mutually exclusive with
    /// <see cref="EventId"/>.</summary>
    public Guid? TreatmentCourseId { get; private set; }

    public WithdrawalTarget Target { get; private set; }
    public DateOnly StartsAt { get; private set; }
    public DateOnly EndsAt { get; private set; }

    private WithdrawalPeriod() { }

    /// <summary>Legacy path: a period anchored to a single <see cref="AnimalEvent"/>
    /// (pre-3.5a.2-B, still used by <c>RecordAnimalEventCommand</c>).</summary>
    public WithdrawalPeriod(Guid animalId, Guid eventId, WithdrawalTarget target, DateOnly startsAt, DateOnly endsAt)
        : this(animalId, eventId, null, target, startsAt, endsAt)
    {
    }

    /// <summary>3.5a.2-B path: a period anchored to a <see cref="TreatmentCourse"/>,
    /// dated from the last application rather than a loose event (task 5).</summary>
    public static WithdrawalPeriod ForTreatmentCourse(
        Guid animalId, Guid treatmentCourseId, WithdrawalTarget target, DateOnly startsAt, DateOnly endsAt)
    {
        if (treatmentCourseId == Guid.Empty)
            throw new DomainException("El período de retiro debe estar asociado a una serie de tratamiento válida.");

        return new WithdrawalPeriod(animalId, null, treatmentCourseId, target, startsAt, endsAt);
    }

    private WithdrawalPeriod(
        Guid animalId, Guid? eventId, Guid? treatmentCourseId, WithdrawalTarget target, DateOnly startsAt, DateOnly endsAt)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("El período de retiro debe estar asociado a un animal.");

        if (eventId == Guid.Empty)
            throw new DomainException("El período de retiro debe estar asociado a un evento.");

        if ((eventId is null) == (treatmentCourseId is null))
            throw new DomainException(
                "El período de retiro debe estar asociado a exactamente un evento o una serie de tratamiento.");

        if (endsAt < startsAt)
            throw new DomainException("La fecha de fin del retiro no puede ser anterior a la fecha de inicio.");

        AnimalId = animalId;
        EventId = eventId;
        TreatmentCourseId = treatmentCourseId;
        Target = target;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public bool IsActiveOn(DateOnly date) => date >= StartsAt && date <= EndsAt;
}
