import { Database } from '@nozbe/watermelondb';

import { Animal, AnimalIdentifier, Pregnancy } from '../database/models';
import { newUuid } from './identifiers';
import { Outbox } from './outbox';

export type Sex = 'M' | 'F';

export interface OffspringInput {
  childId?: string;
  id?: string;
  sex: Sex;
  farmTag?: string;
  birthWeightKg?: number;
  categoryId?: string;
}

export interface RecordBirthInput {
  damId: string;
  pregnancyId?: string;
  offspring: OffspringInput[];
  /** Dual father (ADR-0006): an animal or a straw, never both. */
  sireAnimalId?: string;
  sireStrawId?: string;
  birthDate?: string;
  difficulty?: 'Normal' | 'Assisted' | 'Cesarean' | 'Dystocia';
  bornDead?: number;
  mummified?: number;
  notes?: string;
}

export interface QueuedBirth {
  clientOperationId: string;
  damId: string;
  birthDate: string;
  offspringCount: number;
  offspringIds: string[];
}

/**
 * Registers a birth from the paddock.
 *
 * The entire birth — dam, sire, every calf — travels as a single `recordBirth` operation.
 * That is deliberate: the server routes it to the birthing command, which enrols each calf
 * through the genealogy service. Splitting it into one animal registration per calf, as an
 * earlier version did, loses the parentage without any error being raised, because a plain
 * animal registration has nowhere to put a mother.
 */
export class BirthService {
  private readonly outbox: Outbox;

  constructor(private readonly database: Database) {
    this.outbox = new Outbox(database);
  }

  async recordBirth(input: RecordBirthInput): Promise<QueuedBirth> {
    if (!input.damId) {
      throw new Error('La madre es obligatoria para registrar un parto.');
    }

    if (!input.offspring || input.offspring.length === 0) {
      throw new Error('Debe registrar al menos una cría en el parto.');
    }

    if (input.sireAnimalId && input.sireStrawId) {
      throw new Error('El padre no puede ser un animal y una pajuela al mismo tiempo.');
    }

    const birthDate = input.birthDate ?? new Date().toISOString().slice(0, 10);

    let dam: Animal | undefined;
    try {
      dam = await this.database.get<Animal>('animals').find(input.damId);
      if (dam && !dam.isDeleted) {
        if (dam.sex?.toLowerCase() !== 'female' && dam.sex?.toUpperCase() !== 'F') {
          throw new Error('Solo se pueden registrar partos en animales de sexo hembra.');
        }

        if (dam.disposedAt) {
          const disposedDate = dam.disposedAt.slice(0, 10);
          if (disposedDate <= birthDate) {
            throw new Error('La madre fue dada de baja antes o en la fecha del parto.');
          }
        }
      }
    } catch (err: any) {
      if (err.message?.includes('hembra') || err.message?.includes('dada de baja')) {
        throw err;
      }
    }

    if (input.pregnancyId) {
      try {
        const pregnancy = await this.database.get<Pregnancy>('pregnancies').find(input.pregnancyId);
        if (pregnancy && !pregnancy.isDeleted) {
          if (pregnancy.damId !== input.damId) {
            throw new Error('La preñez seleccionada no corresponde a la madre indicada.');
          }
          if (pregnancy.status !== 'Active') {
            throw new Error('La preñez seleccionada ya fue completada o no está activa.');
          }
        }
      } catch (err: any) {
        if (err.message?.includes('corresponde') || err.message?.includes('activa')) {
          throw err;
        }
      }
    }

    const assignedChildIds = input.offspring.map((calf) => calf.childId ?? calf.id ?? newUuid());

    await this.database.write(async () => {
      const animalsCollection = this.database.get<Animal>('animals');
      const identifiersCollection = this.database.get<AnimalIdentifier>('animal_identifiers');

      for (let i = 0; i < input.offspring.length; i++) {
        const calf = input.offspring[i];
        const childId = assignedChildIds[i];

        await animalsCollection.create((animal) => {
          (animal as any)._raw.id = childId;
          animal.sex = calf.sex === 'M' ? 'Male' : 'Female';
          animal.speciesId = dam?.speciesId ?? '';
          animal.breedId = dam?.breedId;
          animal.categoryId = calf.categoryId;
          animal.motherId = input.damId;
          animal.fatherAnimalId = input.sireAnimalId;
          animal.fatherStrawId = input.sireStrawId;
          animal.birthDate = birthDate;
          animal.isDeleted = false;
          animal.serverCreatedAt = Date.now();
        });

        if (calf.farmTag?.trim()) {
          await identifiersCollection.create((identifier) => {
            identifier.animalId = childId;
            identifier.type = 'FarmTag';
            identifier.value = calf.farmTag!.trim();
            identifier.isActive = true;
            identifier.isDeleted = false;
          });
        }
      }
    });

    const entry = await this.outbox.enqueue(
      'recordBirth',
      {
        damId: input.damId,
        pregnancyId: input.pregnancyId,
        birthDate,
        difficulty: input.difficulty ?? 'Normal',
        bornAlive: input.offspring.length,
        bornDead: input.bornDead ?? 0,
        mummified: input.mummified ?? 0,
        notes: input.notes,
        sireAnimalId: input.sireAnimalId,
        sireStrawId: input.sireStrawId,
        offspring: input.offspring.map((calf, index) => ({
          childId: assignedChildIds[index],
          sex: calf.sex,
          farmTag: calf.farmTag,
          birthWeightKg: calf.birthWeightKg,
          categoryId: calf.categoryId,
        })),
      },
      new Date(`${birthDate}T00:00:00.000Z`).toISOString(),
    );

    return {
      clientOperationId: entry.clientOperationId,
      damId: input.damId,
      birthDate,
      offspringCount: input.offspring.length,
      offspringIds: assignedChildIds,
    };
  }
}
