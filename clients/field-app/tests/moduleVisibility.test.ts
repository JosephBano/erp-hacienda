import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { ModuleVisibility } from '../src/services/moduleVisibility';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';

/**
 * Module visibility per ADR-0019 and docs/spec/plan-0002-fase-3-5/spec.md 3.5a.9-A.
 *
 * The contract is "módulo habilitado AND capacidades AND permisos", evaluated without
 * network so a phone in the paddock does not flash a button in and out of view depending
 * on signal. What this suite pins is the *first* term of that conjunction, the only one
 * this sub-rama introduces.
 */
describe('ModuleVisibility', () => {
  let database: Database;
  let visibility: ModuleVisibility;

  const today = new Date().toISOString().slice(0, 10);
  const recordedBy = 'tester@hato';

  /**
   * The "no network" guarantee of the filter is enforced upstream: this test file does
   * not mock fetch because the visibility service does not call it. The message below
   * makes the intent explicit and turns a future regression into a loud crash.
   */
  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-module-vis-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    visibility = new ModuleVisibility(database);

    global.fetch = jest.fn(() => {
      throw new Error('ModuleVisibility must not call the network.');
    }) as unknown as typeof fetch;
  });

  const giveMilkingSpecies = async (speciesId: string, name: string) => {
    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = name;
        row.gestationDays = 283;
        row.isMilkable = true;
        row.isDeleted = false;
      });
      await database.get('animals').create((row: any) => {
        row._raw.id = `animal-${speciesId}`;
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });
  };

  const seedModule = async (key: string, enabled: boolean) => {
    await database.write(async () => {
      await database.get('farm_modules').create((row: any) => {
        row._raw.id = `fm-${key}`;
        row.key = key;
        row.enabled = enabled;
        row.disabledReason = enabled ? '' : 'piloto porcino';
        row.updatedAt = Date.now();
        row.updatedBy = 'tester@hato';
      });
    });
  };

  it('shows a module by default when no flag row exists yet', async () => {
    // Fresh phone, no pull has run, no row in farm_modules: Production must be visible.
    // "Default visible" preserves the legacy behaviour and keeps the upgrade safe.
    await expect(visibility.canShow('production')).resolves.toBe(true);
  });

  it('hides a module whose enabled flag is false', async () => {
    await seedModule('production', false);

    await expect(visibility.canShow('production')).resolves.toBe(false);
  });

  it('shows a module whose enabled flag is true', async () => {
    await seedModule('production', true);

    await expect(visibility.canShow('production')).resolves.toBe(true);
  });

  it('does not call the network to resolve the flag (Art. 9)', async () => {
    // Reset the global fetch spy before this one assertion so a previous suite's mock
    // does not bleed in.
    (global.fetch as jest.Mock).mockClear();

    await visibility.canShow('production');

    expect(global.fetch).not.toHaveBeenCalled();
  });

  /**
   * ADR-0019 #4: hiding the entry must NOT close the data path. A milking enqueued in
   * the outbox before the module was turned off has to keep going up the wire. This
   * test inverts the people visible/invisible question: even if Production is off, the
   * service still records, the outbox still receives, and the next sync push still has
   * it on the queue.
   */
  it('keeps the milking path live while Production is hidden', async () => {
    await giveMilkingSpecies('species-bovino', 'Bovino');
    await seedModule('production', false);

    const outbox = new Outbox(database);
    const milking = new MilkingService(database);

    await milking.recordIndividualYield('animal-species-bovino', 'Morning', 12.5, recordedBy, today);

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordMilking');
  });

  it('flips the answer when the flag is toggled without network', async () => {
    await seedModule('production', false);
    await expect(visibility.canShow('production')).resolves.toBe(false);

    // Toggle at the local DB to simulate a pull arriving with the new value.
    await database.write(async () => {
      const row = await database.get('farm_modules').find('fm-production');
      await row.update((record: any) => {
        record.enabled = true;
      });
    });

    await expect(visibility.canShow('production')).resolves.toBe(true);
  });

  /**
   * The in-app "Módulos del dispositivo" control flips the flag locally and persists
   * across an immediate re-read. This is what proves "toggle on/off without redeploy"
   * (PLAN 3.5a.9-A) end to end, on the data path that the operator will exercise.
   */
  it('setEnabled persists across a fresh canShow read', async () => {
    await visibility.setEnabled('production', false);
    expect((await database.get('farm_modules').query().fetch()).length).toBe(1);
    await expect(visibility.canShow('production')).resolves.toBe(false);

    await visibility.setEnabled('production', true);
    await expect(visibility.canShow('production')).resolves.toBe(true);
  });

  it('setEnabled is a no-op when the value is already what was requested', async () => {
    await seedModule('production', true);
    await visibility.setEnabled('production', true);
    // Single row, no extras — setting the same value twice does not duplicate.
    expect((await database.get('farm_modules').query().fetch()).length).toBe(1);
  });
});
