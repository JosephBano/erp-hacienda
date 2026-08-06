import { Database, Q } from '@nozbe/watermelondb';

import { MilkYield, OutboxEntryModel, Species, WithdrawalPeriod } from '../database/models';
import { Outbox } from './outbox';

/**
 * The 5 AM flow runs with one hand, gloves on, often no signal. It has to be fast and
 * forgiving. Two layers protect it:
 *
 *   1. **Here (this file).** Blocking-and-lenient: impossible inputs are rejected
 *      before they touch the outbox; improbable ones pass. The reasoning lives in
 *      `assertVolume` and its siblings below. Locked by the
 *      'locks the input guardrail' test in `tests/milking.test.ts`.
 *   2. **3.5a.6 (later).** Plausibility ranges per species, configurable from the
 *      web panel, fail-open when unset. Those are *warnings*, not blocks: the
 *      operator may confirm an unusual figure.
 *
 * The rule both layers obey (PLAN-FASE-3-5-PORCINO 3.5a.0 #4):
 *
 *   "3 taps for the normal, 4 for the rare. Do not punish the operator:
 *    confirm the improbable, block only the impossible."
 *
 * If a future change loosens a block or tightens a confirmation, this comment is
 * the rule it has to justify itself against.
 */
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
    recordedBy: string,
    date: string = todayIso(),
  ): Promise<RecordedYield> {
    assertVolume(liters);

    // Defense-in-depth: the UI disables non-milkable species, but if the picker is
    // bypassed (older cached UI, automated test, a script) the service still rejects.
    // Art. 8: capability is configured per-species, not per-animal.
    await this.assertSpeciesIsMilkable(animalId);

    const status = await this.withdrawalStatus(animalId, date);
    if (status.isWithheld) {
      throw new Error(
        `La leche de este animal está en período de retiro hasta ${status.endsAt} y no es vendible.`,
      );
    }

    const entry = await this.outbox.enqueue('recordMilking', {
      date,
      shift,
      recordedBy,
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
    recordedBy: string,
    date: string = todayIso(),
  ): Promise<RecordedYield> {
    assertVolume(totalLiters);

    const entry = await this.outbox.enqueue('recordMilking', {
      date,
      shift,
      recordedBy,
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

  /**
   * Service-level guard: refuses a milking registration for an animal whose species
   * is flagged `is_milkable = false` (Art. 8: per-species capability, not per-animal).
   * The MilkingScreen already disables non-milkable rows, but if the UI is bypassed
   * (an old cached session, an automated test, a direct service call) this check
   * still rejects the operation with the same wording the UI would have used.
   */
  private async assertSpeciesIsMilkable(animalId: string): Promise<void> {
    let speciesId: string | undefined;
    try {
      const animal = await this.database.get('animals').find(animalId);
      speciesId = (animal as { speciesId?: string }).speciesId;
    } catch {
      throw new Error('No se encontró el animal en este dispositivo.');
    }

    if (!speciesId) {
      throw new Error('El animal no tiene especie asociada. Sincronice para descargar el catálogo.');
    }

    let species: Species | undefined;
    try {
      species = await this.database.get<Species>('species').find(speciesId);
    } catch {
      throw new Error(
        'No se encontró la especie del animal. Sincronice para descargar el catálogo antes de registrar ordeños.',
      );
    }

    if (!species.isMilkable) {
      throw new Error(
        `La especie "${species.name}" no está habilitada para ordeño. Pida al administrador que active esta opción en el panel web.`,
      );
    }
  }
}

/**
 * 3-toque input guard for the milking round (see PLAN-FASE-3-5-PORCINO 3.5a.0 #4):
 *
 *   - `NaN` / `Infinity`              → rejected. A missing or absurd value is a typo.
 *   - `liters < 0`                    → rejected. The sanity floor.
 *   - `liters === 0`                  → rejected. 0 is not a milking; no cow produces
 *                                       exactly nothing. Recording it would litter the
 *                                       outbox with meaningless rows and muddy the
 *                                       plausibility work planned for 3.5a.6.
 *   - `liters > 0`                    → accepted. The plausibility ceiling (a 1000-L cow)
 *                                       is NOT this layer's job; it belongs to the
 *                                       configurable per-species ranges.
 *
 * The error message explains the rule rather than restating it, so an employee who reads
 * it understands *why* their input was rejected and not just that it was.
 */
function assertVolume(liters: number): void {
  if (!Number.isFinite(liters)) {
    throw new Error('El volumen de leche no es un número válido.');
  }
  if (liters < 0) {
    throw new Error('El volumen de leche no puede ser negativo.');
  }
  if (liters === 0) {
    throw new Error(
      '0 litros no es un ordeño: ninguna vaca ordeñada produce exactamente cero. ' +
        'Si la vaca no se ordeñó hoy, no registre ordeño.',
    );
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
