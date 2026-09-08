import { Database, Q } from '@nozbe/watermelondb';

import { Animal, AnimalIdentifier } from '../database/models';
import { Outbox } from './outbox';

export interface AssignIdentifierInput {
  animalId: string;
  type?: string;
  value: string;
  validFrom?: string;
}

export interface QueuedAssignIdentifier {
  clientOperationId: string;
}

/**
 * Service to assign and replace animal identifiers (tags) offline.
 *
 * Implements ADR-0006 / Feature 0007 Commit 3:
 * - Updates local WatermelonDB state: marks previous active tag of same type as inactive,
 *   creates the new identifier row with is_active = true.
 * - Preserves full history (no deletion or in-place overwrite).
 * - Enqueues 'assignAnimalIdentifier' in sync_outbox for idempotent push to backend.
 */
export class IdentifierService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async assignIdentifier(input: AssignIdentifierInput): Promise<QueuedAssignIdentifier> {
    let animal: Animal;
    try {
      animal = await this.database.get<Animal>('animals').find(input.animalId);
    } catch {
      throw new Error('No se encontró el animal en este dispositivo.');
    }

    if (animal.isDeleted) {
      throw new Error('No se encontró el animal en este dispositivo.');
    }

    const type = input.type ?? 'FarmTag';
    const targetType = type.toLowerCase();

    await this.database.write(async () => {
      const existingRows = await this.database
        .get<AnimalIdentifier>('animal_identifiers')
        .query(
          Q.where('animal_id', input.animalId),
          Q.where('is_active', true),
        )
        .fetch();

      for (const row of existingRows) {
        if ((row.type ?? '').toLowerCase() === targetType) {
          await row.update((r) => {
            r.isActive = false;
          });
        }
      }

      await this.database.get<AnimalIdentifier>('animal_identifiers').create((r) => {
        r.animalId = input.animalId;
        r.type = type;
        r.value = input.value;
        r.isActive = true;
        r.isDeleted = false;
      });
    });

    const payload: Record<string, unknown> = {
      animalId: input.animalId,
      type,
      value: input.value,
    };

    if (input.validFrom !== undefined) {
      payload.validFrom = input.validFrom;
    }

    const entry = await this.outbox.enqueue('assignAnimalIdentifier', payload);

    return { clientOperationId: entry.clientOperationId };
  }
}
