import { Database } from '@nozbe/watermelondb';

import { WithdrawalPeriod } from '../database/models';
import { newUuid } from './identifiers';
import { Outbox } from './outbox';

export interface TreatmentInput {
  animalId: string;
  medicationId: string;
  medicationName: string;
  dose: string;
  cost?: number;
  milkWithdrawalDays?: number;
  meatWithdrawalDays?: number;
  notes?: string;
  photoUri?: string;
  occurredAt?: string;
}

/**
 * Input for `recordTreatmentCourse` (3.5a.2-C): the field-app's side of
 * `createTreatmentCourse`, which lands directly in
 * `CreateTreatmentCourseCommand` on the server. **Field names here are the
 * wire contract, not a convenience shape** — `PushSyncCommands.Deserialize`
 * runs with `UnmappedMemberHandling.Disallow`, so a renamed or invented field
 * fails loudly instead of being dropped in silence (the defect this sub-plan
 * exists to close). Keep this interface's keys in lockstep with
 * `CreateTreatmentCourseCommand`'s parameter names (camelCase vs PascalCase
 * only — System.Text.Json matches case-insensitively).
 */
export interface TreatmentCourseInput {
  animalId: string;
  startsAt?: string;
  routeId: string;
  /** Wire-format key from the `treatment_reasons` catalog: scheduled | curative | preventive. */
  reason?: string;
  productId?: string;
  doseKindId: string;
  doseFactorAmount: number;
  doseFactorUnit: string;
  notes?: string;
  administeredDoseAmount?: number;
  administeredDoseUnit?: string;
  applicationNotes?: string;
  milkWithdrawalDays?: number;
  meatWithdrawalDays?: number;
  /**
   * Set when the operator confirmed a dose the local plausibility check
   * (ADR-0022) flagged as improbable. Persisted server-side on the first
   * application's `is_plausibility_confirmed` column.
   */
  isPlausibilityConfirmed?: boolean;
}

export interface WeightInput {
  animalId: string;
  weightKg: number;
  occurredAt?: string;
  notes?: string;
  /**
   * Set by `EventsScreen` when the operator explicitly confirmed a weight the
   * plausibility check (ADR-0022, 3.5a.6) flagged as improbable for this
   * animal's species/category. Carried into the payload so the server-side
   * audit can distinguish a confirmed outlier from an unreviewed one.
   */
  isPlausibilityConfirmed?: boolean;
}

export interface DisposalInput {
  animalId: string;
  causeId: string;
  notes?: string;
  occurredAt?: string;
}

export interface GroupMoveInput {
  animalId: string;
  toGroupId: string;
  fromGroupId?: string;
  movedOn?: string;
  notes?: string;
}

export interface RegisterAnimalInput {
  speciesId: string;
  sex: 'Male' | 'Female';
  birthDate?: string;
  breedId?: string;
  categoryId?: string;
}

/**
 * A group event never names an animal (ADR-0015): "the lot ate 3 sacks" or "12 were
 * sold" is the whole fact. `affectedCount` is required for `Disposal` — it is what the
 * server's `LiveHeadCount` subtracts and what decides whether the lot closes — and
 * optional for the rest (a sample weighing's size lives in `payload` instead).
 */
export interface GroupEventInput {
  groupId: string;
  eventType: 'Weighing' | 'Treatment' | 'Vaccination' | 'Diagnosis' | 'Disposal';
  payload: Record<string, unknown>;
  affectedCount?: number;
  cost?: number;
  occurredAt?: string;
  /**
   * Mortality cause for a lot disposal (3.5a.7 task 2). Carried as its own field —
   * not buried in `payload` — because `RecordGroupEventCommand.CauseId` is what the
   * server validates against the `mortality_causes` catalog and stamps on the row;
   * mirrors how `recordDisposal` (individual) already passes `causeId` top-level.
   */
  causeId?: string;
}

export interface CorrectionInput {
  originalEventId: string;
  reason: string;
  occurredAt?: string;
}

export interface QueuedEvent {
  clientOperationId: string;
}

export interface QueuedAnimal extends QueuedEvent {
  animalId: string;
}

/**
 * Field events: treatments, weighings, moves and the registration of an animal that was
 * never in the system.
 */
export class EventService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async recordTreatment(input: TreatmentInput): Promise<QueuedEvent> {
    if (!input.animalId) throw new Error('El animal es obligatorio.');
    if (!input.medicationId) throw new Error('El medicamento es obligatorio.');

    const occurredAt = input.occurredAt ?? new Date().toISOString();
    const milkWithdrawalDays = input.milkWithdrawalDays ?? 0;
    const meatWithdrawalDays = input.meatWithdrawalDays ?? 0;

    const entry = await this.outbox.enqueue(
      'recordAnimalEvent',
      {
        animalId: input.animalId,
        eventType: 'Treatment',
        occurredAt,
        recordedBy: 'field-app',
        cost: input.cost,
        milkWithdrawalDays,
        meatWithdrawalDays,
        payloadJson: JSON.stringify({
          medicationId: input.medicationId,
          medicationName: input.medicationName,
          dose: input.dose,
          notes: input.notes,
          photoUri: input.photoUri,
          // Uploading needs the attachments module (Fase 4). Saying so explicitly keeps
          // the app from implying a picture is safely on the server when it is not.
          photoUploaded: false,
        }),
      },
      occurredAt,
    );

    await this.applyLocalWithdrawal(input.animalId, occurredAt, milkWithdrawalDays, meatWithdrawalDays);

    return { clientOperationId: entry.clientOperationId };
  }

  /**
   * Records a treatment or vaccination as a `TreatmentCourse` with its first
   * application (3.5a.2-C), the path `VaccinateScreen` and `TreatScreen` both
   * use. Pushed as `createTreatmentCourse`, routed server-side straight into
   * `CreateTreatmentCourseCommand` — see the doc comment on
   * `TreatmentCourseInput` for why the field names here cannot drift from the
   * command's without the push silently dropping data.
   */
  async recordTreatmentCourse(input: TreatmentCourseInput): Promise<QueuedEvent> {
    if (!input.animalId) throw new Error('El animal es obligatorio.');
    if (!input.routeId) throw new Error('La vía de administración es obligatoria.');
    if (!input.doseKindId) throw new Error('La forma de dosis es obligatoria.');
    if (!Number.isFinite(input.doseFactorAmount) || input.doseFactorAmount <= 0) {
      throw new Error('El factor de dosis debe ser mayor que cero.');
    }
    if (!input.doseFactorUnit || input.doseFactorUnit.trim().length === 0) {
      throw new Error('El factor de dosis requiere una unidad explícita (Art. 10).');
    }

    const startsAt = input.startsAt ?? new Date().toISOString();

    const entry = await this.outbox.enqueue(
      'createTreatmentCourse',
      {
        animalId: input.animalId,
        startsAt,
        routeId: input.routeId,
        reason: input.reason,
        productId: input.productId,
        doseKindId: input.doseKindId,
        doseFactorAmount: input.doseFactorAmount,
        doseFactorUnit: input.doseFactorUnit,
        notes: input.notes,
        administeredDoseAmount: input.administeredDoseAmount,
        administeredDoseUnit: input.administeredDoseUnit,
        applicationNotes: input.applicationNotes,
        milkWithdrawalDays: input.milkWithdrawalDays,
        meatWithdrawalDays: input.meatWithdrawalDays,
        isPlausibilityConfirmed: input.isPlausibilityConfirmed ?? false,
      },
      startsAt,
    );

    await this.applyLocalWithdrawal(
      input.animalId,
      startsAt,
      input.milkWithdrawalDays ?? 0,
      input.meatWithdrawalDays ?? 0,
    );

    return { clientOperationId: entry.clientOperationId };
  }

  async recordWeight(input: WeightInput): Promise<QueuedEvent> {
    if (!input.animalId) throw new Error('El animal es obligatorio.');
    if (!Number.isFinite(input.weightKg) || input.weightKg <= 0) {
      throw new Error('El peso debe ser mayor que cero.');
    }

    const occurredAt = input.occurredAt ?? new Date().toISOString();

    const entry = await this.outbox.enqueue(
      'recordAnimalEvent',
      {
        animalId: input.animalId,
        eventType: 'Weighing',
        occurredAt,
        recordedBy: 'field-app',
        payloadJson: JSON.stringify({
          weightKg: input.weightKg,
          notes: input.notes,
          isPlausibilityConfirmed: input.isPlausibilityConfirmed ?? false,
        }),
      },
      occurredAt,
    );

    return { clientOperationId: entry.clientOperationId };
  }

  /**
   * Records an event whose subject is a lot by count, not an animal (ADR-0015). Used for
   * sample weighings, group mortality/disposal, group diagnosis ("one of these is sick,
   * unidentified") and group treatment/vaccination.
   */
  async recordGroupEvent(input: GroupEventInput): Promise<QueuedEvent> {
    if (!input.groupId) throw new Error('El lote es obligatorio.');
    if (input.eventType === 'Disposal' && !(input.affectedCount && input.affectedCount > 0)) {
      throw new Error('Una baja de lote debe declarar cuántas cabezas incluye.');
    }

    const occurredAt = input.occurredAt ?? new Date().toISOString();

    const entry = await this.outbox.enqueue(
      'recordGroupEvent',
      {
        groupId: input.groupId,
        eventType: input.eventType,
        occurredAt,
        recordedBy: 'field-app',
        cost: input.cost,
        affectedCount: input.affectedCount,
        causeId: input.causeId,
        payloadJson: JSON.stringify(input.payload),
      },
      occurredAt,
    );

    return { clientOperationId: entry.clientOperationId };
  }

  /**
   * "Baja con causa" for a single, identified animal (3.5a.3) — the piglet is still
   * within its lactation cohort, still a real row with a real mother, so this is a plain
   * animal-subject event, not a group one. The mother is resolved by the caller from the
   * already-synced herd (Animal.motherId), not looked up here.
   */
  async recordDisposal(input: DisposalInput): Promise<QueuedEvent> {
    if (!input.animalId) throw new Error('El animal es obligatorio.');
    if (!input.causeId) throw new Error('La causa de mortalidad es obligatoria.');

    const occurredAt = input.occurredAt ?? new Date().toISOString();

    const entry = await this.outbox.enqueue(
      'recordAnimalEvent',
      {
        animalId: input.animalId,
        eventType: 'Disposal',
        occurredAt,
        recordedBy: 'field-app',
        causeId: input.causeId,
        payloadJson: JSON.stringify({ notes: input.notes }),
      },
      occurredAt,
    );

    return { clientOperationId: entry.clientOperationId };
  }

  async recordGroupMove(input: GroupMoveInput): Promise<QueuedEvent> {
    if (!input.animalId) throw new Error('El animal es obligatorio.');
    if (!input.toGroupId) throw new Error('El lote de destino es obligatorio.');

    const movedOn = input.movedOn ?? new Date().toISOString().slice(0, 10);

    const entry = await this.outbox.enqueue('moveAnimal', {
      animalId: input.animalId,
      toGroupId: input.toGroupId,
      fromGroupId: input.fromGroupId,
      movedOn,
    });

    return { clientOperationId: entry.clientOperationId };
  }

  /**
   * Records a field correction (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.8, ADR-0017).
   *
   * The original event id is the server's id (the `resultRef` the phone received
   * when the original op was Accepted). The server rejects corrections that
   * arrive on a different UTC calendar day than the original, so the window is
   * local-midnight to UTC-midnight: the phone may not always know the server's
   * offset, but for the local operator "I typed it wrong five minutes ago" is
   * what the day-boundary is asking about.
   */
  async recordCorrection(input: CorrectionInput): Promise<QueuedEvent> {
    if (!input.originalEventId) throw new Error('El evento a corregir es obligatorio.');
    if (!input.reason || input.reason.trim().length === 0) {
      throw new Error('La razón de la corrección es obligatoria.');
    }

    const occurredAt = input.occurredAt ?? new Date().toISOString();

    const entry = await this.outbox.enqueue(
      'recordCorrection',
      {
        originalEventId: input.originalEventId,
        recordedBy: 'field-app',
        reason: input.reason.trim(),
      },
      occurredAt,
    );

    return { clientOperationId: entry.clientOperationId };
  }

  /**
   * Registers an animal that exists in the paddock but not in the system, minting its
   * UUID here (Art. 3) so the employee can weigh or treat it in the same offline session.
   */
  async registerAnimal(input: RegisterAnimalInput): Promise<QueuedAnimal> {
    if (!input.speciesId) throw new Error('La especie es obligatoria.');

    const animalId = newUuid();

    const entry = await this.outbox.enqueue('createAnimal', {
      id: animalId,
      speciesId: input.speciesId,
      sex: input.sex,
      birthDate: input.birthDate,
      breedId: input.breedId,
      categoryId: input.categoryId,
    });

    return { clientOperationId: entry.clientOperationId, animalId };
  }

  /**
   * Writes the withdrawal the phone can compute right now. The server recalculates from
   * the medication master data and sends the authoritative period back on the next pull;
   * until then this is what keeps non-sellable milk from being recorded as sellable.
   */
  private async applyLocalWithdrawal(
    animalId: string,
    occurredAt: string,
    milkDays: number,
    meatDays: number,
  ): Promise<void> {
    if (milkDays <= 0 && meatDays <= 0) return;

    const target = milkDays > 0 && meatDays > 0 ? 'Both' : milkDays > 0 ? 'Milk' : 'Meat';
    const startsAt = occurredAt.slice(0, 10);
    const endsAt = addDays(startsAt, Math.max(milkDays, meatDays));

    await this.database.write(async () => {
      await this.database.get<WithdrawalPeriod>('withdrawal_periods').create((row) => {
        // A local id keeps it distinct from the server's own row, which arrives later
        // through the pull with its own identity.
        row._raw.id = `local-${newUuid()}`;
        row.animalId = animalId;
        row.eventId = 'local';
        row.target = target;
        row.startsAt = startsAt;
        row.endsAt = endsAt;
        row.isDeleted = false;
      });
    });
  }
}

function addDays(isoDate: string, days: number): string {
  const date = new Date(`${isoDate}T00:00:00.000Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}
