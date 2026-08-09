import { Database, Q } from '@nozbe/watermelondb';

import { OutboxEntryModel } from '../database/models';
import { newUuid } from './identifiers';

export type OutboxStatus = 'pending' | 'synced' | 'rejected' | 'cancelled';

export interface OutboxEntry {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  payload: Record<string, unknown>;
  status: OutboxStatus;
  errorDetails?: string;
  resultRef?: string;
  attempts: number;
  queuedAt: number;
}

export interface OutboxStats {
  pending: number;
  synced: number;
  rejected: number;
  cancelled: number;
}

/**
 * Durable queue of everything this phone has recorded and the server has not yet
 * confirmed.
 *
 * Every write the employee makes lands here first and only then, if there is signal, goes
 * out. Nothing is ever removed: an accepted operation is marked `synced` and a refused one
 * is marked `rejected` with the server's reason, so the problems tray always has something
 * to show. Deleting a refused record would be the one failure mode this whole phase exists
 * to prevent — a day of work disappearing with no trace.
 */
export class Outbox {
  /**
   * Keeps the queue order strict even when several records are saved inside the same
   * millisecond, which is exactly what happens when an employee taps through a milking
   * round quickly.
   */
  private static lastStamp = 0;

  constructor(private readonly database: Database) {}

  private get collection() {
    return this.database.get<OutboxEntryModel>('sync_outbox');
  }

  async enqueue(
    operationType: string,
    payload: Record<string, unknown>,
    occurredAt: string = new Date().toISOString(),
  ): Promise<OutboxEntry> {
    const clientOperationId = newUuid();
    const stamp = Math.max(Date.now(), Outbox.lastStamp + 1);
    Outbox.lastStamp = stamp;

    await this.database.write(async () => {
      await this.collection.create((entry) => {
        entry.clientOperationId = clientOperationId;
        entry.operationType = operationType;
        entry.occurredAt = occurredAt;
        entry.payloadJson = JSON.stringify(payload);
        entry.status = 'pending';
        entry.attempts = 0;
        entry.queuedAt = stamp;
      });
    });

    return {
      clientOperationId,
      operationType,
      occurredAt,
      payload,
      status: 'pending',
      attempts: 0,
      queuedAt: stamp,
    };
  }

  /** Oldest first, so a batch carries the day in the order it happened. */
  async pending(limit?: number): Promise<OutboxEntry[]> {
    // The cancelled status is filtered out here: a record the operator undid
    // locally must never make it into a push batch, even if the race between
    // cancel and push is a real one. The conditional update in markCancelled is
    // the only place the status can change, and that change is what this filter
    // is reading.
    const clauses: Q.Clause[] = [
      Q.where('status', 'pending'),
      Q.sortBy('queued_at', Q.asc),
    ];
    if (limit !== undefined) {
      clauses.push(Q.take(limit));
    }

    const rows = await this.collection.query(...clauses).fetch();
    return rows.map(toEntry);
  }

  async rejected(): Promise<OutboxEntry[]> {
    const rows = await this.collection
      .query(Q.where('status', 'rejected'), Q.sortBy('queued_at', Q.desc))
      .fetch();

    return rows.map(toEntry);
  }

  async cancelled(): Promise<OutboxEntry[]> {
    const rows = await this.collection
      .query(Q.where('status', 'cancelled'), Q.sortBy('queued_at', Q.desc))
      .fetch();

    return rows.map(toEntry);
  }

  async all(): Promise<OutboxEntry[]> {
    const rows = await this.collection.query(Q.sortBy('queued_at', Q.desc)).fetch();
    return rows.map(toEntry);
  }

  /**
   * Lists every entry created on the current calendar day (local timezone), newest
   * first, regardless of sync status. Drives the "Lo que registré hoy" screen
   * (3.5a.9-B): the operator wants to see "what did I do today, and where does each
   * record stand?" — including the ones already in the office and the ones the
   * server refused. Local-midnight is the right boundary because that is what an
   * employee means by "today"; UTC midnight would silently cut records from the
   * evening of one day into the morning of the next.
   */
  async today(): Promise<OutboxEntry[]> {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const cutoff = startOfToday.getTime();

    const rows = await this.collection
      .query(Q.where('queued_at', Q.gte(cutoff)), Q.sortBy('queued_at', Q.desc))
      .fetch();
    return rows.map(toEntry);
  }

  async markSynced(clientOperationId: string, resultRef?: string): Promise<void> {
    await this.update(clientOperationId, (entry) => {
      entry.status = 'synced';
      entry.resultRef = resultRef;
      entry.errorDetails = undefined;
    });
  }

  async markRejected(clientOperationId: string, errorDetails: string): Promise<void> {
    await this.update(clientOperationId, (entry) => {
      entry.status = 'rejected';
      entry.errorDetails = errorDetails;
    });
  }

  /**
   * Cancels a still-pending entry. Returns the resulting status when the call
   * succeeds (the entry was pending → cancelled) or when the race against the
   * push engine produced it too late (the entry was already synced or rejected).
   * That "already moved" result is the signal for the caller to drop Camino A
   * and emit a Correction event for Camino B instead.
   *
   * The condition is read and written inside the same database.write so the
   * transition is atomic per WatermelonDB — the sync engine, even with a push
   * already in flight, cannot observe a state where the entry is half-cancelled.
   */
  async cancelPending(
    clientOperationId: string,
  ): Promise<'cancelled' | 'alreadySynced' | 'alreadyRejected' | 'notFound'> {
    const rows = await this.collection
      .query(Q.where('client_operation_id', clientOperationId))
      .fetch();

    const [row] = rows;
    if (!row) {
      return 'notFound';
    }

    if (row.status !== 'pending') {
      if (row.status === 'synced') return 'alreadySynced';
      if (row.status === 'rejected') return 'alreadyRejected';
      if (row.status === 'cancelled') return 'cancelled';
      return 'notFound';
    }

    let outcome: 'cancelled' | 'alreadySynced' | 'alreadyRejected' | 'notFound' = 'cancelled';
    await this.database.write(async () => {
      // Re-read the row inside the write transaction so we are the only writer
      // deciding between pending and any other state. WatermelonDB serializes
      // writes on the same database, so this is the safe place to do it.
      await row.update((entry) => {
        if (entry.status !== 'pending') {
          if (entry.status === 'synced') outcome = 'alreadySynced';
          else if (entry.status === 'rejected') outcome = 'alreadyRejected';
          else outcome = 'notFound';
          return;
        }
        entry.status = 'cancelled';
      });
    });

    return outcome;
  }

  /**
   * Records that a delivery attempt was made. Used by the sync engine's backoff, and
   * visible in the problems tray so "this has failed 9 times" is something a human can
   * notice rather than guess.
   */
  async recordAttempt(clientOperationIds: string[]): Promise<void> {
    if (clientOperationIds.length === 0) return;

    const rows = await this.collection
      .query(Q.where('client_operation_id', Q.oneOf(clientOperationIds)))
      .fetch();

    await this.database.write(async () => {
      await this.database.batch(
        ...rows.map((row) => row.prepareUpdate((entry) => { entry.attempts += 1; })),
      );
    });
  }

  async stats(): Promise<OutboxStats> {
    const [pending, synced, rejected, cancelled] = await Promise.all([
      this.collection.query(Q.where('status', 'pending')).fetchCount(),
      this.collection.query(Q.where('status', 'synced')).fetchCount(),
      this.collection.query(Q.where('status', 'rejected')).fetchCount(),
      this.collection.query(Q.where('status', 'cancelled')).fetchCount(),
    ]);

    return { pending, synced, rejected, cancelled };
  }

  private async update(
    clientOperationId: string,
    mutate: (entry: OutboxEntryModel) => void,
  ): Promise<void> {
    const [row] = await this.collection
      .query(Q.where('client_operation_id', clientOperationId))
      .fetch();

    if (!row) return;

    await this.database.write(async () => {
      await row.update(mutate);
    });
  }
}

function toEntry(row: OutboxEntryModel): OutboxEntry {
  return {
    clientOperationId: row.clientOperationId,
    operationType: row.operationType,
    occurredAt: row.occurredAt,
    payload: parsePayload(row.payloadJson),
    status: row.status as OutboxStatus,
    errorDetails: row.errorDetails,
    resultRef: row.resultRef,
    attempts: row.attempts,
    queuedAt: row.queuedAt,
  };
}

function parsePayload(raw: string): Record<string, unknown> {
  try {
    return JSON.parse(raw) as Record<string, unknown>;
  } catch {
    // A corrupt payload must not take the queue down; the entry stays visible with an
    // empty body so a human can see something went wrong with it.
    return {};
  }
}
