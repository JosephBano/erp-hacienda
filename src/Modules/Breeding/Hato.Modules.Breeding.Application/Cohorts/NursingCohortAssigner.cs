using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Breeding.Domain;
using Hato.Modules.Livestock.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Cohorts;

/// <summary>
/// Decides which nursing cohort a new birthing belongs to, and opens one when there is
/// none (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.4). The rule is: an open cohort of the same
/// species whose latest birth falls inside the species' enrolment window catches the new
/// birthing. Otherwise a fresh cohort is opened. The cohort id is computed here so the
/// command handler never has to remember which branch it took, and the domain invariant
/// ("a cohort never grows past its window") stays in one place.
/// </summary>
public interface INursingCohortAssigner
{
    /// <summary>
    /// Returns the id of the cohort <paramref name="birthDate"/> belongs to, opening a
    /// new one when no candidate cohort is open. Returns null when the species has no
    /// configured window yet — the application layer treats that as "no cohort" and
    /// records the birth without grouping it.
    /// </summary>
    Task<Guid?> AssignAsync(
        Guid speciesId,
        DateOnly birthDate,
        CancellationToken cancellationToken);
}

public class NursingCohortAssigner(
    IBreedingDbContext dbContext,
    IAnimalSpeciesReader speciesReader) : INursingCohortAssigner
{
    public async Task<Guid?> AssignAsync(
        Guid speciesId,
        DateOnly birthDate,
        CancellationToken cancellationToken)
    {
        var profile = await speciesReader.GetLactationProfileAsync(speciesId, cancellationToken);
        if (profile is null)
        {
            // Species was deleted or has no record at all; the rest of the booking pipeline
            // would also fail, so we surface a "no cohort" rather than inventing one.
            return null;
        }

        var windowDays = profile.CohortWindowDays ?? 0;
        if (windowDays <= 0)
        {
            // The species was intentionally configured without an automatic cohort window:
            // a new birthing opens its own cohort by design. This is the safe default
            // when the operator has not yet decided on a number.
            return OpenCohort(speciesId, birthDate);
        }

        // The candidate cohort is the latest open cohort of the species whose earliest
        // birth is within `windowDays` of `birthDate`. "Open" means WeanedAt is null —
        // a closed cohort is historical and never absorbs new births.
        var cutoff = birthDate.AddDays(-windowDays);

        var openCohort = await dbContext.NursingCohorts
            .Where(c => c.SpeciesId == speciesId
                        && c.WeanedAt == null
                        && c.StartedAt >= cutoff
                        && c.StartedAt <= birthDate)
            .OrderByDescending(c => c.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (openCohort is not null)
        {
            return openCohort.Id;
        }

        return OpenCohort(speciesId, birthDate);
    }

    private Guid OpenCohort(Guid speciesId, DateOnly firstBirthDate)
    {
        var cohort = NursingCohort.Open(speciesId, firstBirthDate);
        dbContext.NursingCohorts.Add(cohort);
        return cohort.Id;
    }
}
