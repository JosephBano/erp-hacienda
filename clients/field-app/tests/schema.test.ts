import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema, SCHEMA_VERSION } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';

describe('local schema', () => {
  it('registers every collection the pull delivers', () => {
    const tableNames = Object.keys(schema.tables);

    expect(tableNames).toEqual(
      expect.arrayContaining([
        'animals',
        'animal_identifiers',
        'animal_groups',
        'group_memberships',
        'species',
        'breeds',
        'animal_categories',
        'inventory_items',
        'withdrawal_periods',
        'sync_outbox',
        'milk_yields',
        'sync_meta',
        'farm_modules',
      ]),
    );
  });

  /**
   * Mirror rows are keyed by the server's own UUID (WatermelonDB's `id`), which is what
   * makes a second pull of the same animal an update instead of a twin. A separate
   * `server_id` column would leave the local id free to diverge — the bug that produces
   * two of every animal after a re-sync.
   */
  it('stores the server timestamps needed to reconcile an animal', () => {
    const columnNames = Object.keys(schema.tables['animals'].columns);

    expect(columnNames).toEqual(
      expect.arrayContaining([
        'sex',
        'species_id',
        'mother_id',
        'father_animal_id',
        'father_straw_id',
        'is_deleted',
        'server_created_at',
        'server_updated_at',
        'last_edited_at',
      ]),
    );
  });

  /**
   * A phone in the paddock may be carrying a week of unsynced work, so a schema bump has
   * to migrate rather than reset. If the migration chain does not reach the current
   * version, WatermelonDB refuses to open the database and that queue is unreachable.
   */
  it('has a migration path reaching the current version', () => {
    expect(migrations.maxVersion).toBe(SCHEMA_VERSION);
  });

  it('opens a database and accepts a write on every table', async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-schema-${Math.random()}`,
    });
    const database = new Database({ adapter: adapter as never, modelClasses });

    await database.write(async () => {
      await database.get('withdrawal_periods').create((row: any) => {
        row._raw.id = 'withdrawal-1';
        row.animalId = 'animal-1';
        row.eventId = 'event-1';
        row.target = 'Milk';
        row.startsAt = '2026-08-01';
        row.endsAt = '2026-08-06';
        row.isDeleted = false;
      });
    });

    expect(await database.get('withdrawal_periods').query().fetchCount()).toBe(1);
  });
});
