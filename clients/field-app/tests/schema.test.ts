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
        'mortality_causes',
        'animal_events',
      ]),
    );
  });

  /**
   * 3.5a.1 (ADR-0015) + BACKLOG "AnimalEvent grupal aún no viaja en el pull": the
   * event's subject is exactly one of `animal_id` / `group_id`, mirroring the server's
   * domain invariant and DB CHECK. Both columns must exist and both must be optional —
   * a schema that made `animal_id` required would force a fake value onto every group
   * event, which is precisely the synthetic data ADR-0015 exists to avoid.
   */
  it('lets an event carry an animal subject, a group subject, or neither column filled — never a forced value on the other', () => {
    const columns = schema.tables['animal_events'].columns;

    expect(columns['animal_id'].isOptional).toBe(true);
    expect(columns['group_id'].isOptional).toBe(true);
    expect(Object.keys(columns)).toEqual(
      expect.arrayContaining([
        'animal_id',
        'group_id',
        'event_type',
        'occurred_at',
        'recorded_by',
        'payload_json',
        'affected_count',
        'cause_id',
        'related_event_id',
        'is_deleted',
      ]),
    );
  });

  it('accepts both an animal-subject and a group-subject event row', async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-schema-events-${Math.random()}`,
    });
    const database = new Database({ adapter: adapter as never, modelClasses });

    await database.write(async () => {
      await database.get('animal_events').create((row: any) => {
        row._raw.id = 'evt-animal-1';
        row.animalId = 'animal-1';
        row.groupId = undefined;
        row.eventType = 'Weighing';
        row.occurredAt = '2026-08-01T00:00:00Z';
        row.recordedBy = 'Operario';
        row.payloadJson = '{"kg":45}';
        row.isDeleted = false;
      });

      await database.get('animal_events').create((row: any) => {
        row._raw.id = 'evt-group-1';
        row.animalId = undefined;
        row.groupId = 'group-1';
        row.eventType = 'GroupWeighing';
        row.occurredAt = '2026-08-02T00:00:00Z';
        row.recordedBy = 'Operario';
        row.payloadJson = '{"sample_count":10,"avg_kg":22}';
        row.affectedCount = 42;
        row.isDeleted = false;
      });
    });

    expect(await database.get('animal_events').query().fetchCount()).toBe(2);
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
