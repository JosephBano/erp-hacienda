import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';

/**
 * The outbox is the app's promise to the employee: "what you registered is safe, even
 * with no signal". Before this existed the queue lived in a JavaScript array backed by
 * `localStorage`, which does not exist in React Native — so on a real phone every pending
 * record died when the app was closed.
 *
 * These tests read the queue back through a *new* `Outbox` instance and, where it matters,
 * through a raw database query, which is what catches that class of bug: state kept in a
 * service field passes neither. Durability across an actual process restart belongs to the
 * storage adapter (SQLite on the device) and is verified on hardware, not here.
 */
describe('Outbox', () => {
  let database: Database;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-test-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
  });

  it('stores a queued operation in the database, not in the service', async () => {
    const outbox = new Outbox(database);
    const entry = await outbox.enqueue('recordMilking', { totalLiters: 12 });

    // Straight from the table: nothing here can be served out of the enqueueing
    // instance's memory.
  });

  /**
   * 3.5a.9-B: "Lo que registré hoy" lists the entries created today regardless of
   * status. The day-boundary is local-midnight so an operator who walks past noon is not
   * watching yesterday's list.
   */
  it('today() returns every entry from this calendar day', async () => {
    const outbox = new Outbox(database);

    // Today (queued at "now").
    await outbox.enqueue('recordMilking', { totalLiters: 8 });

    // Yesterday: enqueue first, then rewind the queued_at column directly. The public
    // enqueue API stamps queued_at = Date.now() on purpose (PLAN-FASE-3-4 sec. 2.2) —
    // simulating yesterday needs to bypass that contract.
    await outbox.enqueue('recordMilking', { totalLiters: 5 });
    await database.write(async () => {
      const rows = await database.get('sync_outbox').query().fetch();
      const yesterdayStamp = Date.now() - 24 * 60 * 60 * 1000;
      await rows[1].update((row: any) => {
        row.queuedAt = yesterdayStamp;
      });
    });

    const result = await outbox.today();

    expect(result).toHaveLength(1);
    expect(result[0].payload).toMatchObject({ totalLiters: 8 });
  });

  it('today() newest first', async () => {
    const outbox = new Outbox(database);

    await outbox.enqueue('recordMilking', { totalLiters: 5 }, new Date('2026-08-06T05:00:00.000Z').toISOString());
    await outbox.enqueue('recordMilking', { totalLiters: 7 }, new Date('2026-08-06T15:00:00.000Z').toISOString());

    const result = await outbox.today();

    expect(result.map((e) => e.payload)).toEqual([{ totalLiters: 7 }, { totalLiters: 5 }]);
  });

  it('stores a queued operation in the database, not in the service', async () => {
    const outbox = new Outbox(database);
    const entry = await outbox.enqueue('recordMilking', { totalLiters: 12 });

    // Straight from the table: nothing here can be served out of the enqueueing
    // instance's memory.
    const rows = await database.get('sync_outbox').query().fetch();
    expect(rows).toHaveLength(1);

    const reopened = await new Outbox(database).pending();
    expect(reopened).toHaveLength(1);
    expect(reopened[0].clientOperationId).toBe(entry.clientOperationId);
    expect(reopened[0].payload).toEqual({ totalLiters: 12 });
  });

  it('gives every queued operation its own idempotency key', async () => {
    const outbox = new Outbox(database);

    const first = await outbox.enqueue('recordMilking', { totalLiters: 1 });
    const second = await outbox.enqueue('recordMilking', { totalLiters: 1 });

    expect(first.clientOperationId).not.toBe(second.clientOperationId);
  });

  it('stops offering an operation once the server accepted it', async () => {
    const outbox = new Outbox(database);
    const entry = await outbox.enqueue('createAnimal', { sex: 'Female' });

    await outbox.markSynced(entry.clientOperationId, 'server-ref-1');

    expect(await outbox.pending()).toHaveLength(0);
    expect(await outbox.stats()).toMatchObject({ pending: 0, synced: 1, rejected: 0 });
  });

  /**
   * A rejected operation is never deleted. It moves to the problems tray so a human can
   * see what the farm recorded and the server refused — the alternative is a record that
   * evaporates without anyone noticing.
   */
  it('keeps a rejected operation visible with its reason instead of dropping it', async () => {
    const outbox = new Outbox(database);
    const entry = await outbox.enqueue('recordAnimalEvent', { animalId: 'ghost' });

    await outbox.markRejected(entry.clientOperationId, 'El animal no existe.');

    const rejected = await new Outbox(database).rejected();

    expect(rejected).toHaveLength(1);
    expect(rejected[0].errorDetails).toBe('El animal no existe.');
    expect(await outbox.pending()).toHaveLength(0);
  });

  it('offers pending operations oldest first so a batch preserves the order of the day', async () => {
    const outbox = new Outbox(database);

    const first = await outbox.enqueue('createAnimal', { order: 1 });
    const second = await outbox.enqueue('recordAnimalEvent', { order: 2 });
    const third = await outbox.enqueue('recordMilking', { order: 3 });

    const pending = await outbox.pending();

    expect(pending.map((p) => p.clientOperationId)).toEqual([
      first.clientOperationId,
      second.clientOperationId,
      third.clientOperationId,
    ]);
  });

  it('never hands the pusher more operations than one batch can carry', async () => {
    const outbox = new Outbox(database);

    for (let i = 0; i < 12; i++) {
      await outbox.enqueue('createAnimal', { index: i });
    }

    expect(await outbox.pending(5)).toHaveLength(5);
  });
});
