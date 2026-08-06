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

export interface WeightInput {
  animalId: string;
  weightKg: number;
  occurredAt?: string;
  notes?: string;
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
        payloadJson: JSON.stringify({ weightKg: input.weightKg, notes: input.notes }),
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
