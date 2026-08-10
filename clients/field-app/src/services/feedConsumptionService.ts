import { Database } from '@nozbe/watermelondb';

import { Outbox } from './outbox';

export interface FeedConsumptionInput {
  groupId: string;
  inventoryItemId: string;
  /** The quantity the operator typed, in whatever unit they typed it (e.g. "3 sacos"). */
  quantity: number;
  /**
   * The unit the operator typed (e.g. `saco40kg`). Optional: omitting it means "the
   * item's own base unit", exactly the same contract `RecordGroupFeedConsumptionCommand`
   * uses server-side — see `Hato.Modules.Inventory.Application/Consumptions/
   * RecordGroupFeedConsumptionCommand.cs`.
   */
  unit?: string;
  batchId?: string;
  notes?: string;
  consumedAt?: string;
}

export interface QueuedFeedConsumption {
  clientOperationId: string;
}

/**
 * Consumo de alimento del lote en sacos (3.5a.7 task 5).
 *
 * This is deliberately a separate service from `EventService`: feed consumption is not
 * an `AnimalEvent` (ADR-0015 sujeto lote), it is an `Inventory` module write
 * (`GroupFeedConsumption`) that decrements a batch and lets the cost-prorate engine read
 * kilograms regardless of what unit the operator typed ("bug del saco",
 * PLAN-FASE-3-5-PORCINO.md sec.3.5a.5). The outbox operation type
 * (`recordFeedConsumption`) is routed server-side to `RecordGroupFeedConsumptionCommand`
 * via `PushSyncCommands.cs` — see that file for the exact field names this payload must
 * match, since a misnamed field is dropped silently (no `UnmappedMemberHandling.Disallow`)
 * and the server would still answer `Accepted`.
 */
export class FeedConsumptionService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async recordFeedConsumption(input: FeedConsumptionInput): Promise<QueuedFeedConsumption> {
    if (!input.groupId) throw new Error('El lote es obligatorio.');
    if (!input.inventoryItemId) throw new Error('El alimento es obligatorio.');
    if (!Number.isFinite(input.quantity) || input.quantity <= 0) {
      throw new Error('La cantidad debe ser mayor que cero.');
    }

    const occurredAt = input.consumedAt ?? new Date().toISOString();
    const consumedAt = occurredAt.slice(0, 10);

    const entry = await this.outbox.enqueue(
      'recordFeedConsumption',
      {
        groupId: input.groupId,
        inventoryItemId: input.inventoryItemId,
        quantity: input.quantity,
        consumedAt,
        recordedBy: 'field-app',
        unit: input.unit,
        batchId: input.batchId,
        notes: input.notes,
      },
      occurredAt,
    );

    return { clientOperationId: entry.clientOperationId };
  }
}
