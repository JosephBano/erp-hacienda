import { SyncEngine } from './syncEngine';

export interface OffspringData {
  sex: 'Male' | 'Female';
  farmTag?: string;
  officialTag?: string;
  breedId?: string;
  birthWeightKg?: number;
}

export interface RecordBirthRequest {
  motherId: string;
  speciesId: string;
  fatherAnimalId?: string;
  fatherStrawId?: string;
  birthDate?: string;
  notes?: string;
  offsprings: OffspringData[];
}

export interface LocalBirthResult {
  birthEventOperationId: string;
  createdOffspringIds: string[];
  motherId: string;
  birthDate: string;
}

export class BirthService {
  constructor(private syncEngine: SyncEngine) {}

  async recordBirth(request: RecordBirthRequest): Promise<LocalBirthResult> {
    if (!request.motherId) {
      throw new Error('La madre es obligatoria para el registro de parto.');
    }

    if (!request.speciesId) {
      throw new Error('La especie es obligatoria.');
    }

    if (request.fatherAnimalId && request.fatherStrawId) {
      throw new Error('El padre no puede ser un animal y una pajuela de IA simultáneamente.');
    }

    if (!request.offsprings || request.offsprings.length === 0) {
      throw new Error('Debe registrar al menos una cría en el parto.');
    }

    const birthDate = request.birthDate || new Date().toISOString().split('T')[0];
    const createdOffspringIds: string[] = [];

    // 1. Register each offspring animal via createAnimal operation
    for (const offspring of request.offsprings) {
      const animalPayload = {
        speciesId: request.speciesId,
        sex: offspring.sex,
        birthDate,
        breedId: offspring.breedId,
        motherId: request.motherId,
        fatherAnimalId: request.fatherAnimalId,
        fatherStrawId: request.fatherStrawId,
      };

      const animalOutbox = await this.syncEngine.enqueueOperation('createAnimal', animalPayload);
      createdOffspringIds.push(animalOutbox.clientOperationId);
    }

    // 2. Register Birth event for mother referencing created offsprings
    const birthEventPayload = {
      animalId: request.motherId,
      eventType: 'Birth',
      occurredAt: new Date(birthDate).toISOString(),
      recordedBy: 'field-user',
      payloadJson: JSON.stringify({
        offspringIds: createdOffspringIds,
        fatherAnimalId: request.fatherAnimalId,
        fatherStrawId: request.fatherStrawId,
        notes: request.notes,
      }),
    };

    const birthOutbox = await this.syncEngine.enqueueOperation('recordAnimalEvent', birthEventPayload);

    return {
      birthEventOperationId: birthOutbox.clientOperationId,
      createdOffspringIds,
      motherId: request.motherId,
      birthDate,
    };
  }
}
