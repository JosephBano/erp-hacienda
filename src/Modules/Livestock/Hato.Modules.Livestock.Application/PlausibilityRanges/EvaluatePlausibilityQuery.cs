using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.PlausibilityRanges;

/// <summary>
/// Verdict of a plausibility evaluation (ADR-0022 sec.2).
/// <list type="bullet">
///   <item><term>Pass</term><description>Value within [plausible_min, plausible_max] (or no range configured).</description></item>
///   <item><term>Confirm</term><description>Value outside plausibles but within absolutes. The UI must ask the operator to confirm explicitly.</description></item>
///   <item><term>Block</term><description>Value outside [absolute_min, absolute_max]. The UI must reject the entry; the data is not enqueued.</description></item>
/// </list>
/// </summary>
public enum PlausibilityVerdict
{
    Pass,
    Confirm,
    Block,
}

/// <summary>
/// Evaluates a recorded value against the configured plausibility ranges.
///
/// The lookup prefers a range with a specific <c>category_id</c> over a generic
/// one (<c>category_id IS NULL</c>) for the same (species, magnitude). When no
/// active range matches the combination, the verdict is <see cref="PlausibilityVerdict.Pass"/>
/// — the fail-open behavior of ADR-0022 sec.3. The reason: a range forgotten by
/// the operator must never prevent the recording of a real field datum.
/// </summary>
public record EvaluatePlausibilityQuery(
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal Value) : IRequest<PlausibilityVerdict>;

public class EvaluatePlausibilityHandler(ILivestockDbContext dbContext)
    : IRequestHandler<EvaluatePlausibilityQuery, PlausibilityVerdict>
{
    public async Task<PlausibilityVerdict> Handle(EvaluatePlausibilityQuery request, CancellationToken cancellationToken)
    {
        // Try the most specific range first: (species, category, magnitude).
        // Then the generic one: (species, NULL category, magnitude).
        // If neither exists, fail open (pass).
        var candidates = await dbContext.PlausibilityRanges
            .AsNoTracking()
            .Where(r => r.SpeciesId == request.SpeciesId
                        && r.Magnitude == request.Magnitude
                        && r.IsActive)
            .Where(r => r.CategoryId == request.CategoryId
                        || (request.CategoryId == null && r.CategoryId == null)
                        || (request.CategoryId != null && r.CategoryId == null))
            .Select(r => new
            {
                r.CategoryId,
                r.PlausibleMin,
                r.PlausibleMax,
                r.AbsoluteMin,
                r.AbsoluteMax,
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return PlausibilityVerdict.Pass;

        // Prefer the range with a non-null category when one is provided.
        var range = candidates.FirstOrDefault(c => c.CategoryId == request.CategoryId)
                     ?? candidates.First(c => c.CategoryId == null);

        // Guardrail: any of the bounds is unused on that side. If a bound is
        // null, the verdict on that side is "pass" (fail-open in the small).
        var value = request.Value;

        // Absolute bounds take precedence — a value outside the absolute range
        // is blocked regardless of plausible bounds.
        if (range.AbsoluteMin is { } aMin && value < aMin)
            return PlausibilityVerdict.Block;
        if (range.AbsoluteMax is { } aMax && value > aMax)
            return PlausibilityVerdict.Block;

        // Inside absolutes (or absolutes not configured): check plausibles.
        if (range.PlausibleMin is { } pMin && value < pMin)
            return PlausibilityVerdict.Confirm;
        if (range.PlausibleMax is { } pMax && value > pMax)
            return PlausibilityVerdict.Confirm;

        return PlausibilityVerdict.Pass;
    }
}
