import { EventService } from '../src/services/eventService';
import { SyncEngine } from '../src/services/syncEngine';

describe('EventService Offline Tests', () => {
  let syncEngine: SyncEngine;
  let eventService: EventService;

  beforeEach(() => {
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    syncEngine = new SyncEngine();
    eventService = new EventService(syncEngine);
  });

  test('recordTreatment enqueues event and calculates withdrawal days', async () => {
    const event = await eventService.recordTreatment({
      animalId: 'vaca-01',
      medicationId: 'med-01',
      medicationName: 'Antibiótico X',
      dose: '10ml',
      cost: 15.5,
      milkWithdrawalDays: 5,
      meatWithdrawalDays: 14,
    });

    expect(event.animalId).toBe('vaca-01');
    expect(event.milkWithdrawalDays).toBe(5);
    expect(event.meatWithdrawalDays).toBe(14);
    expect(syncEngine.getOutboxStats().pendingCount).toBe(1);
  });

  test('recordWeight validates positive weight and enqueues operation', async () => {
    await expect(
      eventService.recordWeight({ animalId: 'vaca-01', weightKg: -5 })
    ).rejects.toThrow('El peso debe ser un número positivo.');

    const event = await eventService.recordWeight({ animalId: 'vaca-01', weightKg: 450 });
    expect(event.eventType).toBe('Weight');
    expect(syncEngine.getOutboxStats().pendingCount).toBe(1);
  });

  test('searchAnimalLocal matches tag, name and untagged animals by UUID', () => {
    const animals = [
      { id: 'uuid-001', farmTag: 'F-10', name: 'Manchas' },
      { id: 'uuid-002', officialTag: 'OFF-99' },
      { id: 'uuid-003' }, // Untagged animal (Art. 3)
    ];

    expect(eventService.searchAnimalLocal(animals, 'F-10').length).toBe(1);
    expect(eventService.searchAnimalLocal(animals, 'OFF-99').length).toBe(1);
    expect(eventService.searchAnimalLocal(animals, 'uuid-003').length).toBe(1);
  });

  test('calculateLocalWithdrawalEndDate calculates projected date correctly', () => {
    const endDate = eventService.calculateLocalWithdrawalEndDate('2026-08-01', 5);
    expect(endDate).toBe('2026-08-06');
  });
});
