import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { SyncEngine, backoffDelayMs, applyRow } from '../src/services/syncEngine';
import { LoggerService } from '../src/services/loggerService';
import type { PullResponse, PushResponse, SyncApi } from '../src/services/syncApi';

/**
 * The sync engine is the part that can lose a week of work without anybody noticing, so
 * these tests are written around the failure modes rather than the happy path: no signal,
 * signal that dies mid-push, a server that refuses a record, a record deleted on the
 * server while the phone was away.
 */

class FakeSyncApi implements SyncApi {
  pushCalls: Array<{ clientOperationId: string; operationType: string }[]> = [];
  pullCalls: Array<string | undefined> = [];

  pushHandler: (ops: any[]) => Promise<PushResponse> = async (ops) => ({
    processedCount: ops.length,
    results: ops.map((o) => ({
      clientOperationId: o.clientOperationId,
      status: 'Accepted' as const,
      resultRef: `ref-${o.clientOperationId}`,
      errorDetails: null,
    })),
  });

  pullHandler: (since?: string) => Promise<PullResponse> = async () => ({
    cursor: 'cursor-empty',
    hasMore: false,
    collections: {},
  });

  async push(operations: any[]): Promise<PushResponse> {
    this.pushCalls.push(
      operations.map((o) => ({
        clientOperationId: o.clientOperationId,
        operationType: o.operationType,
      })),
    );
    return this.pushHandler(operations);
  }

  async pull(since?: string): Promise<PullResponse> {
    this.pullCalls.push(since);
    return this.pullHandler(since);
  }
}

describe('SyncEngine', () => {
  let database: Database;
  let outbox: Outbox;
  let api: FakeSyncApi;
  let engine: SyncEngine;

  beforeEach(() => {
    (global as any).__resetNativeMocks?.();

    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-sync-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    api = new FakeSyncApi();
    engine = new SyncEngine(database, api, { batchSize: 3 });
  });

  describe('push', () => {
    it('sends what is queued and stops offering it once accepted', async () => {
      await outbox.enqueue('recordMilking', { totalLiters: 9 });

      await engine.syncNow();

      expect(api.pushCalls).toHaveLength(1);
      expect(await outbox.pending()).toHaveLength(0);
      expect(await outbox.stats()).toMatchObject({ synced: 1, rejected: 0 });
    });

    it('treats a duplicate answer as done, because the record is already on the server', async () => {
      const entry = await outbox.enqueue('createAnimal', { sex: 'Female' });
      api.pushHandler = async (ops) => ({
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Duplicate' as const,
          resultRef: 'existing-ref',
          errorDetails: null,
        })),
      });

      await engine.syncNow();

      expect(await outbox.pending()).toHaveLength(0);
      const [synced] = await outbox.all();
      expect(synced.clientOperationId).toBe(entry.clientOperationId);
      expect(synced.status).toBe('synced');
    });

    it('keeps a duplicate operation rejected when the server returns errorDetails', async () => {
      const entry = await outbox.enqueue('recordMilking', { totalLiters: 4 });
      api.pushHandler = async (ops) => ({
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Duplicate' as const,
          resultRef: null,
          errorDetails: 'La operación fue rechazada previamente en el servidor.',
        })),
      });

      await engine.syncNow();

      expect(await outbox.pending()).toHaveLength(0);
      const rejected = await outbox.rejected();
      expect(rejected).toHaveLength(1);
      expect(rejected[0].clientOperationId).toBe(entry.clientOperationId);
      expect(rejected[0].status).toBe('rejected');
      expect(rejected[0].errorDetails).toBe('La operación fue rechazada previamente en el servidor.');
    });

    /** A refused record is a problem to show a human, never a record to drop. */
    it('keeps a refused operation with the server reason and stops retrying it', async () => {
      await outbox.enqueue('recordAnimalEvent', { animalId: 'ghost' });
      api.pushHandler = async (ops) => ({
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Rejected' as const,
          resultRef: null,
          errorDetails: 'El animal no existe.',
        })),
      });

      await engine.syncNow();
      await engine.syncNow();

      const rejected = await outbox.rejected();
      expect(rejected).toHaveLength(1);
      expect(rejected[0].errorDetails).toBe('El animal no existe.');
      expect(api.pushCalls).toHaveLength(1);
    });

    it('splits the queue into batches the server accepts', async () => {
      for (let i = 0; i < 7; i++) {
        await outbox.enqueue('createAnimal', { index: i });
      }

      await engine.syncNow();

      expect(api.pushCalls.map((call) => call.length)).toEqual([3, 3, 1]);
      expect(await outbox.pending()).toHaveLength(0);
    });

    it('leaves the queue untouched when the push never reaches the server', async () => {
      await outbox.enqueue('recordMilking', { totalLiters: 4 });
      api.pushHandler = async () => {
        throw new Error('Network request failed');
      };

      const result = await engine.syncNow();

      expect(result.ok).toBe(false);
      expect(await outbox.pending()).toHaveLength(1);
      expect(await outbox.stats()).toMatchObject({ pending: 1, synced: 0, rejected: 0 });
    });

    it('converges once the signal comes back', async () => {
      await outbox.enqueue('recordMilking', { totalLiters: 4 });
      api.pushHandler = async () => {
        throw new Error('Network request failed');
      };

      await engine.syncNow();

      api.pushHandler = async (ops) => ({
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Accepted' as const,
          resultRef: 'ok',
          errorDetails: null,
        })),
      });

      const second = await engine.syncNow();

      expect(second.ok).toBe(true);
      expect(await outbox.pending()).toHaveLength(0);
    });

    /**
     * Scenario 10 of docs/spec/plan-0001-fase-3/spec.md sec.2.2, literally: the cut happens *mid* push, after
     * some batches already landed — not before the first one. The employee's first three
     * records must not be re-sent (and thus not risk becoming duplicates) just because
     * the fourth one hit a dead connection.
     */
    it('keeps what already landed when the connection dies partway through a multi-batch push', async () => {
      for (let i = 0; i < 7; i++) {
        await outbox.enqueue('createAnimal', { index: i });
      }

      let batchNumber = 0;
      api.pushHandler = async (ops) => {
        batchNumber += 1;
        if (batchNumber === 2) {
          throw new Error('Network request failed');
        }
        return {
          processedCount: ops.length,
          results: ops.map((o) => ({
            clientOperationId: o.clientOperationId,
            status: 'Accepted' as const,
            resultRef: 'ok',
            errorDetails: null,
          })),
        };
      };

      const first = await engine.syncNow();

      expect(first.ok).toBe(false);
      expect(await outbox.pending()).toHaveLength(4);
      expect(await outbox.stats()).toMatchObject({ pending: 4, synced: 3, rejected: 0 });

      api.pushHandler = async (ops) => ({
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Accepted' as const,
          resultRef: 'ok',
          errorDetails: null,
        })),
      });

      // The three that already landed before the cut must never appear in a later call —
      // a retry legitimately resends the batch that failed, but not the one that didn't.
      const landedIds = new Set(api.pushCalls[0].map((op) => op.clientOperationId));

      const second = await engine.syncNow();

      expect(second.ok).toBe(true);
      expect(await outbox.pending()).toHaveLength(0);
      expect(await outbox.stats()).toMatchObject({ pending: 0, synced: 7, rejected: 0 });

      const idsSentDuringRetry = api.pushCalls.slice(2).flat().map((op) => op.clientOperationId);
      expect(idsSentDuringRetry.some((id) => landedIds.has(id))).toBe(false);
    });

    it('does not contact the server at all with no connectivity', async () => {
      await outbox.enqueue('recordMilking', { totalLiters: 4 });
      (global as any).__setNetworkConnected(false);

      const result = await engine.syncNow();

      expect(result.ok).toBe(false);
      expect(result.reason).toBe('offline');
      expect(api.pushCalls).toHaveLength(0);
      expect(await outbox.pending()).toHaveLength(1);
    });
  });

  describe('pull', () => {
    it('writes what the server sent into the local tables', async () => {
      api.pullHandler = async () => ({
        cursor: 'cursor-1',
        hasMore: false,
        collections: {
          species: [{ id: 'sp-1', name: 'Bovino', gestationDays: 283, isDeleted: false }],
          animals: [
            {
              id: 'an-1',
              sex: 'Female',
              speciesId: 'sp-1',
              motherId: null,
              isDeleted: false,
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: null,
            },
          ],
        },
      });

      await engine.syncNow();

      const animals = await database.get('animals').query().fetch();
      expect(animals).toHaveLength(1);
      expect(animals[0].id).toBe('an-1');
    });

    /**
     * lastEditedAt is the LWW baseline the app must echo back on its next edit
     * (updateAnimal push). Stored as epoch ms like the other server dates, under its
     * own field name — unlike createdAt/updatedAt it has no WatermelonDB-reserved name
     * to dodge.
     */
    it('stores the animal edit baseline so a later edit can declare it', async () => {
      const editedAt = '2026-08-03T06:00:00.000Z';

      api.pullHandler = async () => ({
        cursor: 'cursor-1',
        hasMore: false,
        collections: {
          animals: [
            {
              id: 'an-1',
              sex: 'Female',
              speciesId: 'sp-1',
              isDeleted: false,
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: editedAt,
              lastEditedAt: editedAt,
            },
          ],
        },
      });

      await engine.syncNow();

      const [animal] = await database.get('animals').query().fetch();
      expect((animal as any).lastEditedAt).toBe(Date.parse(editedAt));
    });

    it('leaves the edit baseline unset for an animal that has never been edited', async () => {
      api.pullHandler = async () => ({
        cursor: 'cursor-1',
        hasMore: false,
        collections: {
          animals: [
            {
              id: 'an-1',
              sex: 'Female',
              speciesId: 'sp-1',
              isDeleted: false,
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: null,
              lastEditedAt: null,
            },
          ],
        },
      });

      await engine.syncNow();

      // WatermelonDB may represent "never set" as null rather than undefined depending
      // on the adapter — either is "no baseline", which is the actual contract.
      const [animal] = await database.get('animals').query().fetch();
      expect((animal as any).lastEditedAt).toBeFalsy();
    });

    it('updates an animal it already had instead of storing it twice', async () => {
      const row = {
        id: 'an-1',
        sex: 'Female',
        speciesId: 'sp-1',
        isDeleted: false,
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: null as string | null,
      };

      api.pullHandler = async () => ({
        cursor: 'c1',
        hasMore: false,
        collections: { animals: [row] },
      });
      await engine.syncNow();

      api.pullHandler = async () => ({
        cursor: 'c2',
        hasMore: false,
        collections: {
          animals: [{ ...row, sex: 'Male', updatedAt: '2026-08-02T00:00:00Z' }],
        },
      });
      await engine.syncNow();

      const animals = await database.get('animals').query().fetch();
      expect(animals).toHaveLength(1);
      expect((animals[0] as any).sex).toBe('Male');
    });

    /** Scenario 5 of docs/spec/plan-0001-fase-3/spec.md sec.2.2: a logical delete has to reach the phone. */
    it('removes a record the server marked as deleted', async () => {
      const row = {
        id: 'an-doomed',
        sex: 'Female',
        speciesId: 'sp-1',
        isDeleted: false,
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: null as string | null,
      };

      api.pullHandler = async () => ({
        cursor: 'c1',
        hasMore: false,
        collections: { animals: [row] },
      });
      await engine.syncNow();
      expect(await database.get('animals').query().fetchCount()).toBe(1);

      api.pullHandler = async () => ({
        cursor: 'c2',
        hasMore: false,
        collections: { animals: [{ ...row, isDeleted: true, updatedAt: '2026-08-02T00:00:00Z' }] },
      });
      await engine.syncNow();

      expect(await database.get('animals').query().fetchCount()).toBe(0);
    });

    /**
     * 3.5a.1 (ADR-0015) + BACKLOG "AnimalEvent grupal aún no viaja en el pull": the
     * event history now lands in the local `animal_events` table. Both an
     * animal-subject and a group-subject row must arrive intact, with the XOR
     * honoured — nobody invents a fake `animalId` for the lot event.
     */
    it('writes both animal-subject and group-subject events into the local table', async () => {
      api.pullHandler = async () => ({
        cursor: 'cursor-1',
        hasMore: false,
        collections: {
          animalEvents: [
            {
              id: 'evt-animal-1',
              animalId: 'an-1',
              groupId: null,
              eventType: 'Weighing',
              occurredAt: '2026-08-01T00:00:00Z',
              recordedBy: 'Operario',
              payloadJson: '{"kg":45}',
              affectedCount: null,
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: null,
              isDeleted: false,
            },
            {
              id: 'evt-group-1',
              animalId: null,
              groupId: 'grp-1',
              eventType: 'GroupVaccination',
              occurredAt: '2026-08-02T00:00:00Z',
              recordedBy: 'Operario',
              payloadJson: '{"head_count":42}',
              affectedCount: 42,
              createdAt: '2026-08-02T00:00:00Z',
              updatedAt: null,
              isDeleted: false,
            },
          ],
        },
      });

      await engine.syncNow();

      const events = await database.get('animal_events').query().fetch();
      expect(events).toHaveLength(2);

      const animalEvent = events.find((e) => e.id === 'evt-animal-1') as any;
      expect(animalEvent.animalId).toBe('an-1');
      expect(animalEvent.groupId).toBeFalsy();

      const groupEvent = events.find((e) => e.id === 'evt-group-1') as any;
      expect(groupEvent.groupId).toBe('grp-1');
      expect(groupEvent.animalId).toBeFalsy();
      expect(groupEvent.affectedCount).toBe(42);
    });

    it('resumes from the stored cursor rather than downloading the herd again', async () => {
      api.pullHandler = async () => ({ cursor: 'cursor-42', hasMore: false, collections: {} });

      await engine.syncNow();
      await engine.syncNow();

      expect(api.pullCalls[0]).toBeUndefined();
      expect(api.pullCalls[1]).toBe('cursor-42');
    });

    it('surfaces an unknown collection as a sync failure and logs it via loggerService', async () => {
      const logger = new LoggerService();
      const customEngine = new SyncEngine(database, api, { logger });

      api.pullHandler = async () => ({
        cursor: 'cursor-unknown',
        hasMore: false,
        collections: {
          mysteryCollection: [{ id: 'mystery-1', name: 'Unknown' }],
        },
      });

      const result = await customEngine.syncNow();

      expect(result.ok).toBe(false);
      const errorLogs = logger.getLogs().filter((l) => l.level === 'error');
      expect(errorLogs.some((l) => l.message.includes('mysteryCollection'))).toBe(true);
    });

    it('honours server isDeleted without forcing it to false in applyRow', () => {
      const record: any = { id: 'test-1', isDeleted: false };
      applyRow(record, { id: 'test-1', isDeleted: true });
      expect(record.isDeleted).toBe(true);
    });

    it('keeps following the cursor while the server reports more pages', async () => {
      let call = 0;
      api.pullHandler = async () => {
        call += 1;
        return { cursor: `cursor-${call}`, hasMore: call < 3, collections: {} };
      };

      await engine.syncNow();

      expect(api.pullCalls).toHaveLength(3);
    });

    it('stops reporting success when the pull page budget is exhausted (T3.1 & T3.2)', async () => {
      let call = 0;
      api.pullHandler = async () => {
        call += 1;
        return { cursor: `cursor-${call}`, hasMore: true, collections: {} };
      };

      const result = await engine.syncNow();

      expect(result.ok).toBe(false);
      expect(result.reason).toBe('pending');
      expect(api.pullCalls).toHaveLength(200);
    });

    it('advances the cursor only after applying the page and supports idempotent replay on interruption (T3.3)', async () => {
      const row = {
        id: 'an-replay-test',
        sex: 'Female',
        speciesId: 'sp-1',
        isDeleted: false,
        createdAt: '2026-08-01T00:00:00Z',
        updatedAt: null,
      };

      let step = 0;
      api.pullHandler = async () => {
        step += 1;
        if (step === 1) {
          return { cursor: 'c1', hasMore: true, collections: { animals: [row] } };
        }
        if (step === 2) {
          return { cursor: 'c2', hasMore: true, collections: { animals: [{ ...row, isDeleted: true }] } };
        }
        throw new Error('Network interruption before c3');
      };

      const firstSync = await engine.syncNow();
      expect(firstSync.ok).toBe(false);
      expect(firstSync.reason).toBe('network');

      // Animal was deleted when page 2 applied
      expect(await database.get('animals').query().fetchCount()).toBe(0);

      // Replay from cursor c2: next sync starts with since: 'c2'
      api.pullHandler = async (cursor) => {
        expect(cursor).toBe('c2');
        return { cursor: 'c3', hasMore: false, collections: {} };
      };

      const secondSync = await engine.syncNow();
      expect(secondSync.ok).toBe(true);
      expect(await database.get('animals').query().fetchCount()).toBe(0);
    });
  });

  describe('resetMirror', () => {
    it('clears all mirror tables and pull cursor while leaving outbox intact', async () => {
      // 1. Populate mirror tables and cursor
      api.pullHandler = async () => ({
        cursor: 'cursor-initial',
        hasMore: false,
        collections: {
          species: [{ id: 'sp-1', name: 'Bovino', gestationDays: 283, isDeleted: false }],
          animals: [
            {
              id: 'an-1',
              sex: 'Female',
              speciesId: 'sp-1',
              motherId: null,
              isDeleted: false,
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: null,
            },
          ],
        },
      });
      await engine.syncNow();

      // Verify mirror tables and cursor populated
      expect(await database.get('animals').query().fetchCount()).toBe(1);
      expect(await database.get('species').query().fetchCount()).toBe(1);
      expect(api.pullCalls).toContain(undefined);

      // 2. Populate outbox with pending work
      const outboxEntry = await outbox.enqueue('recordMilking', { totalLiters: 12 });
      expect(await outbox.pending()).toHaveLength(1);

      // 3. Reset mirror
      await engine.resetMirror();

      // 4. Verify mirror tables are empty and cursor is cleared
      expect(await database.get('animals').query().fetchCount()).toBe(0);
      expect(await database.get('species').query().fetchCount()).toBe(0);

      // Verify outbox is 100% intact (Rule 10 & D8)
      const pendingOutbox = await outbox.pending();
      expect(pendingOutbox).toHaveLength(1);
      expect(pendingOutbox[0].clientOperationId).toBe(outboxEntry.clientOperationId);

      // Verify next pull starts from beginning (undefined cursor)
      api.pullCalls = [];
      await engine.syncNow();
      expect(api.pullCalls[0]).toBeUndefined();
    });
  });

  /**
   * Backoff is a pure function so the retry policy can be asserted directly instead of
   * being inferred from timing, which makes for flaky tests and vague guarantees.
   */
  describe('backoffDelayMs', () => {
    it('grows with each consecutive failure', () => {
      expect(backoffDelayMs(0)).toBeLessThan(backoffDelayMs(1));
      expect(backoffDelayMs(1)).toBeLessThan(backoffDelayMs(2));
      expect(backoffDelayMs(2)).toBeLessThan(backoffDelayMs(3));
    });

    it('never waits longer than five minutes, so a phone back in range syncs promptly', () => {
      expect(backoffDelayMs(50)).toBeLessThanOrEqual(5 * 60 * 1000);
    });

    it('starts small enough to feel immediate to the employee', () => {
      expect(backoffDelayMs(0)).toBeLessThanOrEqual(2000);
    });
  });

  describe('scheduling and coordination (Commit 5)', () => {
    it('serialises concurrent sync triggers into a single coordinated execution (T5.1)', async () => {
      let pushCount = 0;
      api.pushHandler = async (ops) => {
        pushCount++;
        await new Promise((resolve) => setTimeout(resolve, 50));
        return {
          processedCount: ops.length,
          results: ops.map((o) => ({
            clientOperationId: o.clientOperationId,
            status: 'Accepted' as const,
            resultRef: 'ok',
            errorDetails: null,
          })),
        };
      };

      await outbox.enqueue('createAnimal', { sex: 'Female' });

      const [res1, res2] = await Promise.all([engine.syncNow(), engine.syncNow()]);

      expect(pushCount).toBe(1);
      expect(res1.ok).toBe(true);
      expect(res2.ok).toBe(true);
      expect(res1).toEqual(res2);
    });

    it('schedules a bounded retry via retryDelayMs on transient failure while active (T5.2)', async () => {
      jest.useFakeTimers();
      try {
        let attempts = 0;
        api.pushHandler = async (ops) => {
          attempts++;
          if (attempts === 1) {
            throw new Error('Network request failed');
          }
          return {
            processedCount: ops.length,
            results: ops.map((o) => ({
              clientOperationId: o.clientOperationId,
              status: 'Accepted' as const,
              resultRef: 'ok',
              errorDetails: null,
            })),
          };
        };

        await outbox.enqueue('recordMilking', { totalLiters: 5 });
        engine.start();

        const firstResult = await engine.syncNow();
        expect(firstResult.ok).toBe(false);
        expect(firstResult.reason).toBe('network');
        expect(attempts).toBe(1);

        // Advance timers by retry delay
        await jest.advanceTimersByTimeAsync(engine.retryDelayMs + 500);

        expect(attempts).toBe(2);
        expect(await outbox.pending()).toHaveLength(0);
      } finally {
        engine.stop();
        jest.useRealTimers();
      }
    });

    it('triggers sync when returning to active foreground state via AppState (T5.3)', async () => {
      let runs = 0;
      api.pullHandler = async () => {
        runs++;
        return { cursor: 'c', hasMore: false, collections: {} };
      };

      engine.start();
      expect(runs).toBe(0);

      (global as any).__setAppState('active');
      await new Promise((resolve) => setTimeout(resolve, 10));

      expect(runs).toBe(1);
      engine.stop();
    });

    it('does not schedule retries for business rejections (T5.4)', async () => {
      jest.useFakeTimers();
      try {
        let pushCalls = 0;
        api.pushHandler = async (ops) => {
          pushCalls++;
          return {
            processedCount: ops.length,
            results: ops.map((o) => ({
              clientOperationId: o.clientOperationId,
              status: 'Rejected' as const,
              resultRef: null,
              errorDetails: 'El animal no existe.',
            })),
          };
        };

        await outbox.enqueue('recordAnimalEvent', { animalId: 'ghost' });
        engine.start();

        const result = await engine.syncNow();
        expect(result.ok).toBe(true);
        expect(result.rejected).toBe(1);
        expect(pushCalls).toBe(1);

        // Advance timers - no auto-retry should occur
        await jest.advanceTimersByTimeAsync(10 * 60 * 1000);
        expect(pushCalls).toBe(1);
      } finally {
        engine.stop();
        jest.useRealTimers();
      }
    });

    it('does not auto-retry on session expiration and preserves local records intact (T5.5)', async () => {
      jest.useFakeTimers();
      try {
        let attempts = 0;
        api.pushHandler = async () => {
          attempts++;
          const err = new Error('Unauthorized');
          err.name = 'AuthenticationExpiredError';
          throw err;
        };

        await outbox.enqueue('recordMilking', { totalLiters: 10 });
        engine.start();

        const result = await engine.syncNow();
        expect(result.ok).toBe(false);
        expect(result.reason).toBe('auth');
        expect(attempts).toBe(1);

        // Advance timers - auth error never auto-retries
        await jest.advanceTimersByTimeAsync(10 * 60 * 1000);
        expect(attempts).toBe(1);

        // Outbox entry is preserved intact
        expect(await outbox.pending()).toHaveLength(1);
      } finally {
        engine.stop();
        jest.useRealTimers();
      }
    });
  });
});
