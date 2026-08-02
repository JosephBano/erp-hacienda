import { BirthService } from '../src/services/birthService';
import { SyncEngine } from '../src/services/syncEngine';

describe('BirthService Offline Tests', () => {
  let syncEngine: SyncEngine;
  let birthService: BirthService;

  beforeEach(() => {
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    syncEngine = new SyncEngine();
    birthService = new BirthService(syncEngine);
  });

  test('recordBirth enqueues createAnimal for offspring and recordAnimalEvent for mother', async () => {
    const result = await birthService.recordBirth({
      motherId: 'madre-01',
      speciesId: 'bovino-sp',
      fatherStrawId: 'pajuela-99',
      offsprings: [{ sex: 'Female', farmTag: 'CRIA-01' }],
    });

    expect(result.motherId).toBe('madre-01');
    expect(result.createdOffspringIds.length).toBe(1);
    // 1 createAnimal + 1 Birth event = 2 outbox operations
    expect(syncEngine.getOutboxStats().pendingCount).toBe(2);
  });

  test('recordBirth rejects dual father specification', async () => {
    await expect(
      birthService.recordBirth({
        motherId: 'madre-01',
        speciesId: 'bovino-sp',
        fatherAnimalId: 'padre-toro',
        fatherStrawId: 'pajuela-99',
        offsprings: [{ sex: 'Male' }],
      })
    ).rejects.toThrow('El padre no puede ser un animal y una pajuela de IA simultáneamente.');
  });

  test('recordBirth supports litters (multiple offsprings) without species if-statements', async () => {
    const result = await birthService.recordBirth({
      motherId: 'cerda-01',
      speciesId: 'porcino-sp',
      offsprings: [
        { sex: 'Male' },
        { sex: 'Female' },
        { sex: 'Female' },
        { sex: 'Male' },
      ],
    });

    expect(result.createdOffspringIds.length).toBe(4);
    // 4 createAnimal + 1 Birth event = 5 outbox operations
    expect(syncEngine.getOutboxStats().pendingCount).toBe(5);
  });
});
