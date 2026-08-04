import { Database, Q } from '@nozbe/watermelondb';

import { MilkYield, OutboxEntryModel, WithdrawalPeriod } from '../database/models';
import { Outbox } from './outbox';

export type MilkingShift = 'Morning' | 'Afternoon' | 'Evening';

export interface RecordedYield {
  clientOperationId: string;
  animalId?: string;
  groupId?: string;
  shift: MilkingShift;
  liters: number;
  date: string;
}

export interface DailySummary {
  date: string;
  totalLiters: number;
  recordsCount: number;
  yields: RecordedYield[];
}

export interface WithdrawalStatus {
  isWithheld: boolean;
  endsAt?: string;
}

const MILK_TARGETS = new Set(['Milk', 'Both']);

/**
 * The daily milking round, offline.
 *
 * The withdrawal check (Art. 19) is answered from the `withdrawal_periods` table the pull
 * fills, not from a list the caller has to remember to load. That distinction is the whole
 * point: at 5 AM in a paddock there is nobody to load it, and a block that only works
 * online is not a block.
 */
export class MilkingService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async recordIndividualYield(
    animalId: string,
    shift: MilkingShift,
    liters: number,
    date: string = todayIso(),
  ): Promise<RecordedYield> {
    assertVolume(liters);

    const status = await this.withdrawalStatus(animalId, date);
    if (status.isWithheld) {
      throw new Error(
        `La leche de este animal está en período de retiro hasta ${status.endsAt} y no es vendible.`,
      );
    }

    const entry = await this.outbox.enqueue('recordMilking', {
      date,
      shift,
      totalLiters: liters,
      individualYields: [{ animalId, liters }],
    });

    return this.remember({
      clientOperationId: entry.clientOperationId,
      animalId,
      shift,
      liters,
      date,
    });
  }

  async recordGroupMilking(
    groupId: string,
    shift: MilkingShift,
    totalLiters: number,
    date: string = todayIso(),
  ): Promise<RecordedYield> {
    assertVolume(totalLiters);

    const entry = await this.outbox.enqueue('recordMilking', {
      date,
      shift,
      groupId,
      totalLiters,
    });

    return this.remember({
      clientOperationId: entry.clientOperationId,
      groupId,
      shift,
      liters: totalLiters,
      date,
    });
  }

  /** Whether this animal's milk is non-sellable on the given date. */
  async withdrawalStatus(animalId: string, date: string = todayIso()): Promise<WithdrawalStatus> {
    const periods = await this.database
      .get<WithdrawalPeriod>('withdrawal_periods')
      .query(Q.where('animal_id', animalId))
      .fetch();

    const active = periods.find(
      (period) =>
        MILK_TARGETS.has(period.target) &&
        !period.isDeleted &&
        period.startsAt <= date &&
        period.endsAt >= date,
    );

    return active ? { isWithheld: true, endsAt: active.endsAt } : { isWithheld: false };
  }

  async dailySummary(date: string = todayIso()): Promise<DailySummary> {
    const rows = await this.database
      .get<MilkYield>('milk_yields')
      .query(Q.where('date', date))
      .fetch();

    const yields = rows.map(toRecordedYield);

    return {
      date,
      recordsCount: yields.length,
      totalLiters: round2(yields.reduce((sum, entry) => sum + entry.liters, 0)),
      yields,
    };
  }

  /**
   * Fixes a figure that has not left the phone. Once the server has it, Art. 1 applies:
   * the correction is a new event, never an edit of the old one.
   */
  async correctTodayYield(clientOperationId: string, liters: number): Promise<void> {
    assertVolume(liters);

    const [entry] = await this.database
      .get<OutboxEntryModel>('sync_outbox')
      .query(Q.where('client_operation_id', clientOperationId))
      .fetch();

    if (!entry) {
      throw new Error('No se encontró el registro de ordeño.');
    }

    if (entry.status !== 'pending') {
      throw new Error(
        'El registro ya fue sincronizado: una corrección debe registrarse como un evento nuevo.',
      );
    }

    const [record] = await this.database
      .get<MilkYield>('milk_yields')
      .query(Q.where('client_operation_id', clientOperationId))
      .fetch();

    if (record && record.date !== todayIso()) {
      throw new Error('Solo se puede corregir el ordeño del día en curso.');
    }

    const payload = JSON.parse(entry.payloadJson) as Record<string, unknown>;
    payload.totalLiters = liters;
    if (Array.isArray(payload.individualYields) && payload.individualYields.length === 1) {
      (payload.individualYields as Array<Record<string, unknown>>)[0].liters = liters;
    }

    await this.database.write(async () => {
      await entry.update((row) => {
        row.payloadJson = JSON.stringify(payload);
      });

      if (record) {
        await record.update((row) => {
          row.liters = liters;
        });
      }
    });
  }

  private async remember(record: RecordedYield): Promise<RecordedYield> {
    await this.database.write(async () => {
      await this.database.get<MilkYield>('milk_yields').create((row) => {
        row.clientOperationId = record.clientOperationId;
        row.animalId = record.animalId;
        row.groupId = record.groupId;
        row.shift = record.shift;
        row.liters = record.liters;
        row.date = record.date;
      });
    });

    return record;
  }
}

function assertVolume(liters: number): void {
  if (!Number.isFinite(liters) || liters < 0) {
    throw new Error('El volumen de leche debe ser mayor o igual a cero.');
  }
}

function toRecordedYield(row: MilkYield): RecordedYield {
  return {
    clientOperationId: row.clientOperationId,
    animalId: row.animalId,
    groupId: row.groupId,
    shift: row.shift as MilkingShift,
    liters: row.liters,
    date: row.date,
  };
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}
