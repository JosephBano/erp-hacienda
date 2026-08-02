import { SyncEngine } from './syncEngine';

export interface TreatmentEventRequest {
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

export interface WeightEventRequest {
  animalId: string;
  weightKg: number;
  occurredAt?: string;
  notes?: string;
}

export interface GroupMoveEventRequest {
  animalId: string;
  fromGroupId?: string;
  toGroupId: string;
  occurredAt?: string;
  notes?: string;
}

export interface LocalAnimalEvent {
  clientOperationId: string;
  animalId: string;
  eventType: string;
  occurredAt: string;
  cost?: number;
  milkWithdrawalDays?: number;
  meatWithdrawalDays?: number;
  photoUri?: string;
}

export class EventService {
  private localEvents: LocalAnimalEvent[] = [];

  constructor(private syncEngine: SyncEngine) {}

  async recordTreatment(request: TreatmentEventRequest): Promise<LocalAnimalEvent> {
    if (!request.animalId) throw new Error('El animal es obligatorio.');
    if (!request.medicationId) throw new Error('El medicamento es obligatorio.');

    const milkWithdrawalDays = request.milkWithdrawalDays || 0;
    const meatWithdrawalDays = request.meatWithdrawalDays || 0;

    const payload = {
      animalId: request.animalId,
      eventType: 'Treatment',
      occurredAt: request.occurredAt || new Date().toISOString(),
      recordedBy: 'field-user',
      payloadJson: JSON.stringify({
        medicationId: request.medicationId,
        medicationName: request.medicationName,
        dose: request.dose,
        notes: request.notes,
        photoUri: request.photoUri,
      }),
      cost: request.cost,
      milkWithdrawalDays,
      meatWithdrawalDays,
    };

    const outboxItem = await this.syncEngine.enqueueOperation('recordAnimalEvent', payload, request.occurredAt);

    const localEvent: LocalAnimalEvent = {
      clientOperationId: outboxItem.clientOperationId,
      animalId: request.animalId,
      eventType: 'Treatment',
      occurredAt: payload.occurredAt,
      cost: request.cost,
      milkWithdrawalDays,
      meatWithdrawalDays,
      photoUri: request.photoUri,
    };

    this.localEvents.push(localEvent);
    return localEvent;
  }

  async recordWeight(request: WeightEventRequest): Promise<LocalAnimalEvent> {
    if (!request.animalId) throw new Error('El animal es obligatorio.');
    if (request.weightKg <= 0) throw new Error('El peso debe ser un número positivo.');

    const payload = {
      animalId: request.animalId,
      eventType: 'Weight',
      occurredAt: request.occurredAt || new Date().toISOString(),
      recordedBy: 'field-user',
      payloadJson: JSON.stringify({
        weightKg: request.weightKg,
        notes: request.notes,
      }),
    };

    const outboxItem = await this.syncEngine.enqueueOperation('recordAnimalEvent', payload, request.occurredAt);

    const localEvent: LocalAnimalEvent = {
      clientOperationId: outboxItem.clientOperationId,
      animalId: request.animalId,
      eventType: 'Weight',
      occurredAt: payload.occurredAt,
    };

    this.localEvents.push(localEvent);
    return localEvent;
  }

  async recordGroupMove(request: GroupMoveEventRequest): Promise<LocalAnimalEvent> {
    if (!request.animalId) throw new Error('El animal es obligatorio.');
    if (!request.toGroupId) throw new Error('El grupo destino es obligatorio.');

    const payload = {
      animalId: request.animalId,
      eventType: 'GroupMove',
      occurredAt: request.occurredAt || new Date().toISOString(),
      recordedBy: 'field-user',
      payloadJson: JSON.stringify({
        fromGroupId: request.fromGroupId,
        toGroupId: request.toGroupId,
        notes: request.notes,
      }),
    };

    const outboxItem = await this.syncEngine.enqueueOperation('recordAnimalEvent', payload, request.occurredAt);

    const localEvent: LocalAnimalEvent = {
      clientOperationId: outboxItem.clientOperationId,
      animalId: request.animalId,
      eventType: 'GroupMove',
      occurredAt: payload.occurredAt,
    };

    this.localEvents.push(localEvent);
    return localEvent;
  }

  calculateLocalWithdrawalEndDate(startDateStr: string, withdrawalDays: number): string {
    const start = new Date(startDateStr);
    start.setDate(start.getDate() + withdrawalDays);
    return start.toISOString().split('T')[0];
  }

  searchAnimalLocal(animals: Array<{ id: string; farmTag?: string; officialTag?: string; name?: string }>, query: string) {
    const q = query.toLowerCase().trim();
    if (!q) return animals;

    return animals.filter(
      (a) =>
        a.id.toLowerCase().includes(q) ||
        a.farmTag?.toLowerCase().includes(q) ||
        a.officialTag?.toLowerCase().includes(q) ||
        a.name?.toLowerCase().includes(q)
    );
  }
}
