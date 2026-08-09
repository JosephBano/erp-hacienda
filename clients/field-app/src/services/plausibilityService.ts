import { Database } from '@nozbe/watermelondb';
import { PlausibilityRange } from '../database/models';

/**
 * The three verdicts a recorded value can receive (ADR-0022 sec.2).
 *
 *   pass    — value within [plausible_min, plausible_max] (or no range
 *             configured at all). The operator proceeds without friction.
 *   confirm — value outside plausibles but inside absolutes. The UI must
 *             show a confirmation dialog; if the operator confirms, the
 *             event is enqueued with is_plausibility_confirmed = true.
 *   block   — value outside [absolute_min, absolute_max]. The UI rejects
 *             the entry; the data is not enqueued.
 */
export type PlausibilityVerdict = 'pass' | 'confirm' | 'block';

export interface PlausibilityInput {
  speciesId: string;
  categoryId?: string | null;
  magnitude: string;
  value: number;
}

/**
 * Fail-open offline plausibility check (ADR-0022 sec.3).
 *
 * Contract:
 * - If the local plausibility_ranges table is empty (no seed, no pull yet)
 *   OR no row matches the (species, category, magnitude) combination,
 *   the verdict is 'pass'. The cost of a missed value (a real datum the
 *   operator cannot register) is higher than the cost of an accepted
 *   outlier; the sync to the server is the second line of defense.
 * - When a category-specific row and a species-wide (category_id IS NULL)
 *   row both exist, the category-specific one wins — same precedence as
 *   the server-side resolver.
 */
export async function evaluatePlausibility(
  database: Database,
  input: PlausibilityInput,
): Promise<PlausibilityVerdict> {
  const rows = await database
    .get<PlausibilityRange>('plausibility_ranges')
    .query()
    .fetch();

  const candidates = rows.filter(
    (r) =>
      !r.isDeleted &&
      r.isActive &&
      r.speciesId === input.speciesId &&
      r.magnitude === input.magnitude &&
      (r.categoryId === input.categoryId ||
        (input.categoryId == null && r.categoryId == null) ||
        (input.categoryId != null && r.categoryId == null)),
  );

  if (candidates.length === 0) {
    return 'pass';
  }

  // Prefer category-specific over species-wide.
  const range =
    candidates.find((c) => c.categoryId === input.categoryId) ??
    candidates.find((c) => c.categoryId == null);

  if (!range) {
    return 'pass';
  }

  const value = input.value;

  // Absolute bounds take precedence over plausible bounds.
  if (range.absoluteMin != null && value < range.absoluteMin) return 'block';
  if (range.absoluteMax != null && value > range.absoluteMax) return 'block';

  // Inside absolutes (or absolutes not configured): check plausibles.
  if (range.plausibleMin != null && value < range.plausibleMin) return 'confirm';
  if (range.plausibleMax != null && value > range.plausibleMax) return 'confirm';

  return 'pass';
}
