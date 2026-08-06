import { Database } from '@nozbe/watermelondb';

import { Animal } from '../database/models';
import { Outbox } from './outbox';

export interface EditAnimalInput {
  animalId: string;
  /** `undefined` leaves the field as it is queued; `null` explicitly clears it. */
  breedId?: string | null;
  categoryId?: string | null;
  birthDate?: string | null;
}

export interface QueuedAnimalEdit {
  clientOperationId: string;
}

/**
 * Edits an animal's mutable fields — the one editable-entity flow the LWW mechanism
 * (ADR-0008) exists for. Everything else in the sync protocol is append-only events,
 * where two devices cannot collide on the same field.
 *
 * Deliberately does **not** apply the edit to the local record. Whether this device's
 * edit wins or loses is a question only the server can answer (it needs to see what, if
 * anything, another device wrote in the meantime); showing the edit locally before that
 * answer arrives risks displaying a value that a losing LWW resolution never actually
 * produced. The next pull brings the resolved truth, win or lose.
 */
export class AnimalEditService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async editAnimal(input: EditAnimalInput): Promise<QueuedAnimalEdit> {
    let animal: Animal;
    try {
      animal = await this.database.get<Animal>('animals').find(input.animalId);
    } catch {
      throw new Error('No se encontró el animal en este dispositivo.');
    }

    // The device's honest claim about what it last saw: a real instant if it knows the
    // animal has been edited before, or null if it knows the animal has never been
    // edited — both are meaningful answers to the server's conflict check.
    const knownUpdatedAt = animal.lastEditedAt ? new Date(animal.lastEditedAt).toISOString() : null;

    const entry = await this.outbox.enqueue('updateAnimal', {
      animalId: input.animalId,
      breedId: input.breedId ?? null,
      categoryId: input.categoryId ?? null,
      birthDate: input.birthDate ?? null,
      knownUpdatedAt,
    });

    return { clientOperationId: entry.clientOperationId };
  }
}
