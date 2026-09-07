import { Database, Q } from '@nozbe/watermelondb';
import NetInfo from '@react-native-community/netinfo';

import { AppState, type NativeEventSubscription } from 'react-native';

import { SyncMeta } from '../database/models';
import { Outbox, OutboxStats } from './outbox';
import { LoggerService } from './loggerService';
import type { PushOperation, PushResult, SyncApi } from './syncApi';

const CURSOR_KEY = 'pull_cursor';
const MAX_PULL_PAGES = 200;

export type SyncFailureReason = 'offline' | 'network' | 'auth' | 'unknown' | 'pending';

export interface SyncResult {
  ok: boolean;
  reason?: SyncFailureReason;
  pushed: number;
  rejected: number;
  pulled: number;
  stats: OutboxStats;
}

export type SyncChangeListener = (result: SyncResult) => void;

export interface SyncEngineOptions {
  /** Must stay at or below the server's own batch ceiling (500). */
  batchSize?: number;
  logger?: LoggerService;
}

/**
 * Delay before the next attempt after `consecutiveFailures` failed ones.
 *
 * Exponential, because a phone out of range will not come back sooner for being asked
 * more often, and every failed attempt costs battery. Capped at five minutes so that a
 * phone that *has* come back into range does not sit on a long-grown delay while the
 * employee waits — and jittered so a whole crew arriving at the milking parlour at once
 * does not hit the server in lockstep.
 */
export function backoffDelayMs(consecutiveFailures: number): number {
  const base = Math.min(1000 * 2 ** consecutiveFailures, 5 * 60 * 1000);
  const jitter = base * 0.2 * Math.random();
  return Math.min(base + jitter, 5 * 60 * 1000);
}

/**
 * Drives the offline protocol from the phone's side: push what the employee recorded,
 * then pull what changed on the farm.
 *
 * Push runs before pull on purpose. What this device recorded is the only copy that
 * exists; what the server has is safe either way.
 */
export class SyncEngine {
  private readonly outbox: Outbox;
  private readonly batchSize: number;
  private readonly logger: LoggerService;
  private readonly changeListeners = new Set<SyncChangeListener>();
  private consecutiveFailures = 0;
  private running = false;
  private inFlightSync?: Promise<SyncResult>;
  private inFlightReset?: Promise<void>;

  get isRunning(): boolean {
    return this.running;
  }
  private retryTimer?: ReturnType<typeof setTimeout>;
  private isStarted = false;
  private unsubscribeNetInfo?: () => void;
  private appStateSubscription?: NativeEventSubscription | { remove: () => void };

  constructor(
    private readonly database: Database,
    private readonly api: SyncApi,
    options: SyncEngineOptions = {},
  ) {
    this.outbox = new Outbox(database);
    this.batchSize = options.batchSize ?? 100;
    this.logger = options.logger ?? new LoggerService();
  }

  /**
   * Subscribes to changes applied by synchronization (e.g. pushes acknowledged, pulls applied).
   * Notifies container and screens so open views reflect confirmed database state.
   */
  subscribe(listener: SyncChangeListener): () => void {
    this.changeListeners.add(listener);
    return () => {
      this.changeListeners.delete(listener);
    };
  }

  private notifyChanges(result: SyncResult): void {
    for (const listener of this.changeListeners) {
      try {
        listener(result);
      } catch (error) {
        this.logger.error('Error in sync change listener', { error: String(error) });
      }
    }
  }

  /**
   * Opportunistic syncing: run when the signal returns or when returning to foreground (AppState).
   * Also schedules retries on transient failures while active.
   */
  start(): void {
    this.isStarted = true;
    this.unsubscribeNetInfo = NetInfo.addEventListener((state) => {
      if (state.isConnected) {
        void this.syncNow();
      }
    });

    this.appStateSubscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        void this.syncNow();
      }
    });
  }

  stop(): void {
    this.isStarted = false;
    this.clearRetryTimer();
    this.unsubscribeNetInfo?.();
    this.unsubscribeNetInfo = undefined;
    this.appStateSubscription?.remove();
    this.appStateSubscription = undefined;
  }

  private clearRetryTimer(): void {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = undefined;
    }
  }

  private scheduleRetry(): void {
    this.clearRetryTimer();
    if (!this.isStarted) return;

    const delay = this.retryDelayMs;
    this.retryTimer = setTimeout(() => {
      this.retryTimer = undefined;
      void this.syncNow();
    }, delay);
    if (typeof (this.retryTimer as any)?.unref === 'function') {
      (this.retryTimer as any).unref();
    }
  }

  /** Milliseconds the caller should wait before trying again, given the failures so far. */
  get retryDelayMs(): number {
    return backoffDelayMs(this.consecutiveFailures);
  }

  async stats(): Promise<OutboxStats> {
    return this.outbox.stats();
  }

  async syncNow(): Promise<SyncResult> {
    if (this.inFlightReset) {
      try {
        await this.inFlightReset;
      } catch {
        // Handled by resetMirror caller
      }
    }

    if (this.inFlightSync) {
      return this.inFlightSync;
    }

    this.clearRetryTimer();
    this.running = true;
    this.inFlightSync = this.performSync();
    try {
      const result = await this.inFlightSync;
      if (result.pulled > 0 || result.pushed > 0 || result.rejected > 0) {
        this.notifyChanges(result);
      }
      if (!result.ok) {
        // Transient network failures or pending pull pages are scheduled for retry
        // while the app is active. Business rejections, offline states (handled by
        // NetInfo listener), and expired sessions are not auto-retried.
        if (result.reason !== 'offline' && result.reason !== 'auth') {
          this.scheduleRetry();
        }
      }
      return result;
    } finally {
      this.running = false;
      this.inFlightSync = undefined;
    }
  }

  private async performSync(): Promise<SyncResult> {
    const netState = await NetInfo.fetch();
    if (!netState.isConnected) {
      return this.result(false, 'offline', 0, 0, 0);
    }

    const push = await this.pushOutbox();
    if (!push.ok) {
      this.consecutiveFailures += 1;
      return this.result(false, push.reason, push.pushed, push.rejected, 0);
    }

    const pull = await this.pullChanges();
    if (!pull.ok) {
      if (pull.reason !== 'pending') {
        this.consecutiveFailures += 1;
      }
      return this.result(false, pull.reason, push.pushed, push.rejected, pull.pulled);
    }

    this.consecutiveFailures = 0;
    return this.result(true, undefined, push.pushed, push.rejected, pull.pulled);
  }

  private async pushOutbox(): Promise<{
    ok: boolean;
    reason?: SyncFailureReason;
    pushed: number;
    rejected: number;
  }> {
    let pushed = 0;
    let rejected = 0;

    for (;;) {
      const batch = await this.outbox.pending(this.batchSize);
      if (batch.length === 0) {
        return { ok: true, pushed, rejected };
      }

      const operations: PushOperation[] = batch.map((entry) => ({
        clientOperationId: entry.clientOperationId,
        operationType: entry.operationType,
        occurredAt: entry.occurredAt,
        payload: entry.payload,
      }));

      await this.outbox.recordAttempt(batch.map((entry) => entry.clientOperationId));

      let results: PushResult[];
      try {
        results = (await this.api.push(operations)).results;
      } catch (error) {
        // The batch stays pending, untouched. Anything else would risk discarding work
        // that never reached the server.
        return { ok: false, reason: classify(error), pushed, rejected };
      }

      for (const result of results) {
        if (result.status === 'Rejected') {
          rejected += 1;
          await this.outbox.markRejected(
            result.clientOperationId,
            result.errorDetails ?? 'El servidor rechazó la operación sin indicar el motivo.',
          );
          continue;
        }

        if (result.status === 'Duplicate' && result.errorDetails) {
          // If a duplicate returns errorDetails, the operation was previously rejected
          // on the server. Replaying it must preserve the rejection rather than whitewashing it.
          rejected += 1;
          await this.outbox.markRejected(result.clientOperationId, result.errorDetails);
          continue;
        }

        // Accepted (and Duplicate without errorDetails, meaning previously accepted)
        // are successful outcomes: the record was applied on the server.
        pushed += 1;
        await this.outbox.markSynced(result.clientOperationId, result.resultRef ?? undefined);
      }

      // A server that answered about nothing would loop forever otherwise.
      if (results.length === 0) {
        return { ok: false, reason: 'unknown', pushed, rejected };
      }
    }
  }

  /**
   * Resets all mirror tables (those populated from the server via TABLE_BY_COLLECTION)
   * and clears the stored pull cursor so the next sync performs a clean full download.
   *
   * Coordinates mutually exclusively with syncNow (running / inFlightSync).
   *
   * CRITICAL (Rule 10 & D8): NEVER touches sync_outbox, milk_yields, or any local-only tables.
   */
  async resetMirror(): Promise<void> {
    if (this.inFlightReset) {
      return this.inFlightReset;
    }

    const resetPromise = (async () => {
      if (this.inFlightSync) {
        try {
          await this.inFlightSync;
        } catch {
          // Handled by syncNow caller
        }
      }

      this.running = true;
      try {
        await this.performResetMirror();
      } finally {
        this.running = false;
        this.inFlightReset = undefined;
      }
    })();

    this.inFlightReset = resetPromise;
    return resetPromise;
  }

  private async performResetMirror(): Promise<void> {
    const mirrorTables = Array.from(new Set(Object.values(TABLE_BY_COLLECTION)));

    await this.database.write(async () => {
      for (const table of mirrorTables) {
        const collection = this.database.get(table);
        const records = await collection.query().fetch();
        const destroyOps = records.map((r) => r.prepareDestroyPermanently());
        if (destroyOps.length > 0) {
          await this.database.batch(...destroyOps);
        }
      }

      const metaCollection = this.database.get<SyncMeta>('sync_meta');
      const metaRecords = await metaCollection.query(Q.where('key', CURSOR_KEY)).fetch();
      const metaDestroyOps = metaRecords.map((r) => r.prepareDestroyPermanently());
      if (metaDestroyOps.length > 0) {
        await this.database.batch(...metaDestroyOps);
      }
    });
  }

  private async pullChanges(): Promise<{
    ok: boolean;
    reason?: SyncFailureReason;
    pulled: number;
  }> {
    let cursor = await this.readCursor();
    let pulled = 0;

    for (let page = 0; page < MAX_PULL_PAGES; page++) {
      let response;
      try {
        response = await this.api.pull(cursor, this.batchSize);
      } catch (error) {
        return { ok: false, reason: classify(error), pulled };
      }

      try {
        pulled += await this.applyCollections(response.collections);
      } catch (error) {
        return { ok: false, reason: classify(error), pulled };
      }

      cursor = response.cursor || cursor;
      if (cursor) {
        await this.writeCursor(cursor);
      }

      if (!response.hasMore) {
        return { ok: true, pulled };
      }
    }

    return { ok: false, reason: 'pending', pulled };
  }

  /**
   * Writes server rows into the mirror tables, keyed by the server's id so a repeated
   * delivery updates rather than duplicates, and honouring tombstones so a record deleted
   * on the farm stops appearing in the employee's lists.
   */
  private async applyCollections(
    collections: Record<string, Array<Record<string, unknown>>>,
  ): Promise<number> {
    let applied = 0;
    let hasUnknown = false;

    for (const [collectionName, rows] of Object.entries(collections)) {
      const table = TABLE_BY_COLLECTION[collectionName];
      if (!table) {
        this.logger.logError(`Colección no reconocida en sincronización: ${collectionName}`, {
          collection: collectionName,
          count: Array.isArray(rows) ? rows.length : 0,
        });
        hasUnknown = true;
        continue;
      }

      if (!Array.isArray(rows) || rows.length === 0) {
        continue;
      }

      const ids = rows.map((row) => String(row.id));
      const existing = await this.database.get(table).query(Q.where('id', Q.oneOf(ids))).fetch();
      const byId = new Map(existing.map((record) => [record.id, record]));

      const operations = rows.map((row) => {
        const id = String(row.id);
        const current = byId.get(id);

        if (row.isDeleted === true) {
          return current ? current.prepareDestroyPermanently() : null;
        }

        if (current) {
          return current.prepareUpdate((record: any) => applyRow(record, row));
        }

        return this.database.get(table).prepareCreate((record: any) => {
          record._raw.id = id;
          applyRow(record, row);
        });
      });

      // The local array is called `ops` (not `batch`) because WatermelonDB exposes a
      // method `database.batch(...)` for executing prepared operations. Naming our
      // local variable `batch` produced a `ReferenceError: Property 'batch' doesn't
      // exist` at runtime when the for-of loop continued past the database.write()
      // call — the inner WatermelonDB method's closure was shadowing our variable
      // in the bundle Metro produced. Renaming sidesteps it without changing the
      // public API.
      const ops = operations.filter(Boolean) as any[];
      if (ops.length === 0) continue;

      await this.database.write(async () => {
        await this.database.batch(...ops);
      });

      applied += ops.length;
    }

    if (hasUnknown) {
      throw new Error('Colección no reconocida recibida durante la sincronización');
    }

    return applied;
  }

  private async readCursor(): Promise<string | undefined> {
    const [row] = await this.database
      .get<SyncMeta>('sync_meta')
      .query(Q.where('key', CURSOR_KEY))
      .fetch();

    return row?.value || undefined;
  }

  private async writeCursor(cursor: string): Promise<void> {
    if (!cursor) return;

    const collection = this.database.get<SyncMeta>('sync_meta');
    const [row] = await collection.query(Q.where('key', CURSOR_KEY)).fetch();

    await this.database.write(async () => {
      if (row) {
        await row.update((meta) => {
          meta.value = cursor;
        });
        return;
      }

      await collection.create((meta) => {
        meta.key = CURSOR_KEY;
        meta.value = cursor;
      });
    });
  }

  private async result(
    ok: boolean,
    reason: SyncFailureReason | undefined,
    pushed: number,
    rejected: number,
    pulled: number,
  ): Promise<SyncResult> {
    return { ok, reason, pushed, rejected, pulled, stats: await this.outbox.stats() };
  }
}

const TABLE_BY_COLLECTION: Record<string, string> = {
  animals: 'animals',
  animalIdentifiers: 'animal_identifiers',
  animalGroups: 'animal_groups',
  groupMemberships: 'group_memberships',
  species: 'species',
  breeds: 'breeds',
  animalCategories: 'animal_categories',
  inventoryItems: 'inventory_items',
  withdrawalPeriods: 'withdrawal_periods',
  mortalityCauses: 'mortality_causes',
  // ADR-0019: the server's module on/off rows arrive through the same pull as
  // every other collection. The local model already exists (3.5a.9-A), so the
  // pull just upserts new rows and overwrites old ones — same one-shot, no
  // special path.
  farmModules: 'farm_modules',
  // 3.5a.2-A: catalog of administration routes (oral_water, im, sc, ...).
  administrationRoutes: 'administration_routes',
  // 3.5a.2-A: catalog of treatment reasons (scheduled, curative, preventive).
  treatmentReasons: 'treatment_reasons',
  // 3.5a.2-B/C: dose-form catalog (absolute, per_weight, per_head).
  doseKinds: 'dose_kinds',
  // 3.5a.6 (ADR-0022): plausibility ranges for offline validation.
  plausibilityRanges: 'plausibility_ranges',
  // 3.5a.1 (ADR-0015) + BACKLOG "AnimalEvent grupal aún no viaja en el pull":
  // animal- and group-subject event history, needed by 3.5a.7's lot record.
  animalEvents: 'animal_events',
  pregnancies: 'pregnancies',
  breedingServices: 'breeding_services',
};

/**
 * Maps the server's camelCase payload onto the model's fields. `createdAt`/`updatedAt`
 * are stored under `server*` names because WatermelonDB reserves the plain ones for its
 * own bookkeeping.
 */
export function applyRow(record: any, row: Record<string, unknown>): void {
  for (const [key, value] of Object.entries(row)) {
    if (key === 'id') continue;

    if (key === 'createdAt' || key === 'updatedAt' || key === 'lastEditedAt') {
      // Server dates arrive as ISO strings; WatermelonDB number columns need epoch ms.
      // lastEditedAt keeps its own name (unlike created/updatedAt) because there is no
      // WatermelonDB-reserved field it collides with.
      const target = key === 'createdAt' ? 'serverCreatedAt' : key === 'updatedAt' ? 'serverUpdatedAt' : 'lastEditedAt';
      if (target in record) {
        record[target] = value ? Date.parse(String(value)) : undefined;
      }
      continue;
    }

    if (key in record) {
      record[key] = value ?? undefined;
    }
  }
}

function classify(error: unknown): SyncFailureReason {
  const name = (error as Error)?.name;
  if (name === 'AuthenticationExpiredError') return 'auth';

  const message = String((error as Error)?.message ?? '');
  if (/network|fetch|timeout|ECONN/i.test(message)) return 'network';

  return 'unknown';
}
