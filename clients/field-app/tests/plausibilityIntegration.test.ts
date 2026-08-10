import { evaluatePlausibility, PlausibilityVerdict } from '../src/services/plausibilityService';

/**
 * Smoke test that the plausibility round-trip is idempotent: a value
 * that passes once passes on a second evaluation, and the verdict
 * type narrows correctly. This guards the integration with the
 * TreatScreen, which evaluates twice in the confirm-dialog
 * flow (once before showing the dialog, once after the user accepts).
 */

describe('plausibility integration smoke', () => {
  it('re-evaluating a value yields the same verdict', async () => {
    const database = {
      get: (_table: string) => ({
        query: () => ({
          fetch: async () => [{
            speciesId: 's1',
            categoryId: 'c1',
            magnitude: 'dose_ml',
            plausibleMin: 1,
            plausibleMax: 100,
            absoluteMin: 0.1,
            absoluteMax: 200,
            isActive: true,
            isDeleted: false,
          }],
        }),
      }),
    } as unknown as Parameters<typeof evaluatePlausibility>[0];

    const first = await evaluatePlausibility(database, {
      speciesId: 's1', categoryId: 'c1', magnitude: 'dose_ml', value: 50,
    });
    const second = await evaluatePlausibility(database, {
      speciesId: 's1', categoryId: 'c1', magnitude: 'dose_ml', value: 50,
    });
    expect(first).toBe(second);
    expect(first).toBe<PlausibilityVerdict>('pass');
  });
});
