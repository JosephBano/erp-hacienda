import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { BirthService } from '../src/services/birthService';
import { Outbox } from '../src/services/outbox';
import { loadActiveHerd, loadHerd } from '../src/services/herdQueries';

describe('BirthService — Offline-first offspring identity (ADR-0027, T4.1, T4.3, T4.6)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: BirthService;

  const speciesId = 'species-bovine-1';
  const damId = 'dam-cow-1';

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-birth-service-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new BirthService(database);

    // Seed species and dam
    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = 'Bovino';
        row.isMilkable = true;
      });

      await database.get('animals').create((row: any) => {
        row._raw.id = damId;
        row.speciesId = speciesId;
        row.sex = 'Female';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });
  });

  it('mints client UUID for each offspring and makes them immediately queryable in loadHerd / loadActiveHerd (T4.1, T4.3, T4.6)', async () => {
    const queued = await service.recordBirth({
      damId,
      birthDate: '2026-09-08',
      offspring: [
        { sex: 'F', farmTag: 'CRIA-01', birthWeightKg: 35 },
        { sex: 'M' }, // Untagged male
      ],
    });

    expect(queued.offspringIds).toHaveLength(2);
    const [femaleId, maleId] = queued.offspringIds;
    expect(femaleId).toBeDefined();
    expect(maleId).toBeDefined();
    expect(femaleId).not.toEqual(maleId);

    // Verify outbox entry carries childId for each calf
    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordBirth');
    const offspringPayload = (pending[0].payload as any).offspring;
    expect(offspringPayload).toHaveLength(2);
    expect(offspringPayload[0].childId).toBe(femaleId);
    expect(offspringPayload[0].farmTag).toBe('CRIA-01');
    expect(offspringPayload[1].childId).toBe(maleId);

    // Verify immediately queryable in loadHerd without any sync
    const herd = await loadHerd(database);
    const femaleInHerd = herd.find((m) => m.animalId === femaleId);
    const maleInHerd = herd.find((m) => m.animalId === maleId);

    expect(femaleInHerd).toBeDefined();
    expect(femaleInHerd?.sex).toBe('Female');
    expect(femaleInHerd?.motherId).toBe(damId);
    expect(femaleInHerd?.speciesId).toBe(speciesId);
    expect(femaleInHerd?.tag).toBe('CRIA-01');
    expect(femaleInHerd?.label).toBe('CRIA-01');
    expect(femaleInHerd?.hasPendingTag).toBe(false);

    expect(maleInHerd).toBeDefined();
    expect(maleInHerd?.sex).toBe('Male');
    expect(maleInHerd?.motherId).toBe(damId);
    expect(maleInHerd?.hasPendingTag).toBe(true);
    expect(maleInHerd?.label).toBe(`Sin arete · ${maleId.slice(-6)}`);

    // Verify immediately queryable in loadActiveHerd
    const activeHerd = await loadActiveHerd(database);
    expect(activeHerd.some((m) => m.animalId === femaleId)).toBe(true);
    expect(activeHerd.some((m) => m.animalId === maleId)).toBe(true);
  });

  it('respects client-supplied childId or id if passed in offspring input', async () => {
    const customChildId = '11111111-2222-3333-4444-555555555555';
    const queued = await service.recordBirth({
      damId,
      offspring: [{ childId: customChildId, sex: 'F', farmTag: 'CRIA-CUSTOM' }],
    });

    expect(queued.offspringIds).toEqual([customChildId]);

    const herd = await loadHerd(database);
    const calf = herd.find((m) => m.animalId === customChildId);
    expect(calf).toBeDefined();
    expect(calf?.tag).toBe('CRIA-CUSTOM');
  });
});
