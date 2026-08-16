using Hato.SharedKernel;

namespace Hato.Modules.Breeding.Domain;

/// <summary>
/// A group of litters born in consecutive days that are managed together as a single
/// lactation unit (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.4).
///
/// The client's rule of thumb is that litters are weaned together when the LAST litter of
/// the cohort reaches weaning age — not litter by litter. That is the whole reason this
/// aggregate exists: <c>weaning_date = max(birth_date of its Litters) + days_of_lactation</c>,
/// with the days configurable per species rather than a 24 constant baked into code,
/// matching the Art. 8 pattern followed by <c>GestationDays</c>.
///
/// A cohort is automatically opened when a new birthing lands: if a cohort of the same
/// species is open and the birth date falls inside its enrolment window (litter window,
/// also per species), the birthing joins it; otherwise a new cohort is started. Litters
/// are never re-assigned between cohorts once added: the cohort is the historical truth
/// of "which litters were nursing together", and changing that retroactively would
/// rewrite the weaning day that the farmer already saw on the calendar.
///
/// After weaning the cohort advances to a final state on
/// <see cref="MarkSorted"/> (3.5a.4 task 4 / ADR-0023): the day the weaned
/// piglets are mixed into the headcount engorde lots, with one row per piglet
/// (WeightSorted) and one row per destination lot (GroupWeightSorting) already
/// emitted and the GroupMemberships already created. The cohort never moves
/// again after this mark — sorting is one-shot.
/// </summary>
public class NursingCohort : AuditableEntity
{
    public Guid SpeciesId { get; private set; }
    public DateOnly StartedAt { get; private set; }
    public DateOnly? ClosedAt { get; private set; }
    public DateOnly? WeanedAt { get; private set; }
    public DateOnly? SortedAt { get; private set; }
    public string? Notes { get; private set; }

    private NursingCohort() { } // EF Core

    private NursingCohort(Guid speciesId, DateOnly startedAt, string? notes)
    {
        Id = Guid.NewGuid();
        SpeciesId = speciesId;
        StartedAt = startedAt;
        Notes = notes?.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Opens a cohort anchored to the first birth of a new lactation wave. The
    /// identifier is generated here so the caller can attach the birthing without
    /// racing the EF Core insert.
    /// </summary>
    public static NursingCohort Open(Guid speciesId, DateOnly firstBirthDate, string? notes = null)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("La cohorte de lactancia debe estar asociada a una especie.");
        if (firstBirthDate == default)
            throw new DomainException("La fecha de inicio de la cohorte no puede estar vacía.");

        return new NursingCohort(speciesId, firstBirthDate, notes);
    }

    /// <summary>
    /// The teaching date derives from the LATEST birth in the cohort plus the
    /// species' lactation length. The latest birth is what the client watches to
    /// decide the day of weaning; this method does the arithmetic so the application
    /// layer does not need to know about it.
    /// </summary>
    public DateOnly ComputeWeaningDate(int daysOfLactation, DateOnly latestBirthDate)
    {
        if (daysOfLactation <= 0)
            throw new DomainException("Los días de lactancia deben ser mayores a cero.");
        if (latestBirthDate < StartedAt)
            throw new DomainException(
                "La fecha del parto más reciente no puede ser anterior al inicio de la cohorte.");

        return latestBirthDate.AddDays(daysOfLactation);
    }

    /// <summary>
    /// Closes the cohort when weaning is recorded. <paramref name="weaningDate"/> is the
    /// actual day the operation took place (it may differ from the computed day by a day
    /// or two — the field wins, the formula is informational). Closing is one-shot: a
    /// cohort does not get re-opened. Mixing the two would make the pre-weaning
    /// mortality KPI double-count deaths, which is the regression Room 6 caught in
    /// the design review.
    /// </summary>
    public void RecordWeaning(DateOnly weaningDate, int weanedCount, string? notes = null)
    {
        if (WeanedAt is not null)
            throw new DomainException("Esta cohorte ya tiene un destete registrado.");
        if (ClosedAt is not null)
            throw new DomainException("Esta cohorte ya está cerrada.");
        if (weaningDate < StartedAt)
            throw new DomainException("La fecha de destete no puede ser anterior al inicio de la cohorte.");
        if (weanedCount < 0)
            throw new DomainException("La cantidad destetada no puede ser negativa.");

        // The weanedCount parameter is part of the API contract for symmetry with the
        // per-birthing RecordWeaning signature, but the cohort does not aggregate it:
        // Art. 1 puts the truth on each individual Birthing (which validates
        // weanedCount <= BornAlive) and the cohort carries only the date.

        WeanedAt = weaningDate;
        ClosedAt = weaningDate;
        if (!string.IsNullOrWhiteSpace(notes))
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | Destete: {notes.Trim()}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Closes the weaned-to-mixed transition (3.5a.4 task 4, ADR-0023). Marks the day
    /// the weaned piglets were sorted by weight into headcount engorde lots — the
    /// handler that calls this has already emitted the N individual
    /// <c>WeightSorted</c> and M <c>GroupWeightSorting</c> events and created the
    /// N <c>GroupMembership</c> rows that move the animals from "in a nursing
    /// cohort" to "in an engorde lot".
    ///
    /// Two invariants keep the model honest:
    /// <list type="bullet">
    /// <item><b>Post-weaning only.</b> A cohort is sorted AFTER it was weaned.
    /// Sorting unweaned piglets would skip the per-birthing weaning event and
    /// leave the preweaning mortality math (3.5a.3) silently wrong.</item>
    /// <item><b>One-shot.</b> A cohort is sorted exactly once. Re-sorting the
    /// same cohort would double the <c>WeightSorted</c> rows and the
    /// <c>GroupMembership</c> rows; the only legitimate use of the
    /// classification endpoint a second time would be a manual undo, which
    /// the system does not support (Art. 1: the past is not edited).</item>
    /// </list>
    /// </summary>
    public void MarkSorted(DateOnly sortedAt, string? notes = null)
    {
        if (WeanedAt is null)
            throw new DomainException(
                "La cohorte aún no ha sido destetada; no se puede clasificar por peso.");
        if (SortedAt is not null)
            throw new DomainException(
                $"La cohorte ya fue clasificada por peso el {SortedAt:yyyy-MM-dd}.");
        if (sortedAt < StartedAt)
            throw new DomainException(
                "La fecha de clasificación no puede ser anterior al inicio de la cohorte.");

        SortedAt = sortedAt;
        if (!string.IsNullOrWhiteSpace(notes))
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | Clasificación: {notes.Trim()}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
