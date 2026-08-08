import { Database } from '@nozbe/watermelondb';
import { evaluatePlausibility, PlausibilityVerdict } from '../src/services/plausibilityService';

/**
 * Tests for the fail-open plausibility evaluator (3.5a.6, ADR-0022).
 *
 * The function is the single source of truth for the field app's
 * treatment / vaccination flow: it gates the confirm dialog and
 * protects against biologically impossible values. Tests cover the
 * three states (pass/confirm/block) plus the fail-open contract that
 * is the design's central point.
 */

interface MockRange {
  speciesId: string;
  categoryId?: string;
  magnitude: string;
  plausibleMin?: number;
  plausibleMax?: number;
  absoluteMin?: number;
  absoluteMax?: number;
  isActive: boolean;
  isDeleted: boolean;
}

function makeDatabase(ranges: MockRange[]): Database {
  return {
    get: (_table: string) => ({
      query: () => ({
        fetch: async () => ranges as never,
      }),
    }),
  } as unknown as Database;
}

describe('evaluatePlausibility', () => {
  const speciesId = 'species-1';
  const categoryId = 'category-1';
  const magnitude = 'weight_kg';

  it('returns pass when no range matches the combination (fail-open)', async () => {
    const database = makeDatabase([]);
    const verdict = await evaluatePlausibility(database, {
      speciesId,
      categoryId,
      magnitude,
      value: 1000,
    });
    expect(verdict).toBe<PlausibilityVerdict>('pass');
  });

  it('returns pass for a value inside plausible bounds', async () => {
    const database = makeDatabase([
      {
        speciesId, categoryId, magnitude,
        plausibleMin: 0.5, plausibleMax: 250,
        absoluteMin: 0.1, absoluteMax: 500,
        isActive: true, isDeleted: false,
      },
    ]);
    const verdict = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 100,
    });
    expect(verdict).toBe<PlausibilityVerdict>('pass');
  });

  it('returns confirm for a value outside plausibles but inside absolutes', async () => {
    const database = makeDatabase([
      {
        speciesId, categoryId, magnitude,
        plausibleMin: 0.5, plausibleMax: 250,
        absoluteMin: 0.1, absoluteMax: 500,
        isActive: true, isDeleted: false,
      },
    ]);
    const verdict = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 350,
    });
    expect(verdict).toBe<PlausibilityVerdict>('confirm');
  });

  it('returns block for a value outside absolutes', async () => {
    const database = makeDatabase([
      {
        speciesId, categoryId, magnitude,
        plausibleMin: 0.5, plausibleMax: 250,
        absoluteMin: 0.1, absoluteMax: 500,
        isActive: true, isDeleted: false,
      },
    ]);
    const verdict = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 600,
    });
    expect(verdict).toBe<PlausibilityVerdict>('block');
  });

  it('prefers a category-specific range over a species-wide one', async () => {
    const database = makeDatabase([
      {
        // Species-wide: 0–80 plausible
        speciesId, categoryId: undefined, magnitude,
        plausibleMin: 0, plausibleMax: 80,
        absoluteMin: 0, absoluteMax: 200,
        isActive: true, isDeleted: false,
      },
      {
        // Category-specific: 30–50 plausible (so 25 is outside)
        speciesId, categoryId, magnitude,
        plausibleMin: 30, plausibleMax: 50,
        absoluteMin: 0, absoluteMax: 200,
        isActive: true, isDeleted: false,
      },
    ]);
    const withCategory = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 25,
    });
    expect(withCategory).toBe<PlausibilityVerdict>('confirm');

    const withoutCategory = await evaluatePlausibility(database, {
      speciesId, categoryId: null, magnitude, value: 25,
    });
    expect(withoutCategory).toBe<PlausibilityVerdict>('pass');
  });

  it('returns pass for a soft-deleted range (fail-open)', async () => {
    const database = makeDatabase([
      {
        speciesId, categoryId, magnitude,
        plausibleMin: 0.5, plausibleMax: 250,
        absoluteMin: 0.1, absoluteMax: 500,
        isActive: true, isDeleted: true,
      },
    ]);
    const verdict = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 1000,
    });
    expect(verdict).toBe<PlausibilityVerdict>('pass');
  });

  it('returns pass for an inactive range (fail-open)', async () => {
    const database = makeDatabase([
      {
        speciesId, categoryId, magnitude,
        plausibleMin: 0.5, plausibleMax: 250,
        absoluteMin: 0.1, absoluteMax: 500,
        isActive: false, isDeleted: false,
      },
    ]);
    const verdict = await evaluatePlausibility(database, {
      speciesId, categoryId, magnitude, value: 1000,
    });
    expect(verdict).toBe<PlausibilityVerdict>('pass');
  });
});
