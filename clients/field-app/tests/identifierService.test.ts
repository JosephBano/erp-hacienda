import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses, AnimalIdentifier } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { IdentifierService } from '../src/services/identifierService';

describe('IdentifierService (feature-0007 Commit 3)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: IdentifierService;

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-identifier-service-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new IdentifierService(database);

    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'animal-1';
        row.sex = 'Female';
        row.speciesId = 'species-1';
        row.isDeleted = false;
        row.serverCreatedAt = Date.parse('2026-08-01T00:00:00.000Z');
      });
    });
  });

  it('assigns and replaces tags offline in WatermelonDB with history preservation (T3.1, T3.4)', async () => {
    // 1. Assign first tag offline
    const result1 = await service.assignIdentifier({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-101',
    });
    expect(result1.clientOperationId).toBeDefined();

    const identifiers1 = await database.get<AnimalIdentifier>('animal_identifiers').query().fetch();
    expect(identifiers1.length).toBe(1);
    expect(identifiers1[0].animalId).toBe('animal-1');
    expect(identifiers1[0].type).toBe('FarmTag');
    expect(identifiers1[0].value).toBe('TAG-101');
    expect(identifiers1[0].isActive).toBe(true);
    expect(identifiers1[0].isDeleted).toBe(false);

    // 2. Replace tag offline with a new value
    const result2 = await service.assignIdentifier({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-102',
      validFrom: '2026-09-08',
    });
    expect(result2.clientOperationId).toBeDefined();
    expect(result2.clientOperationId).not.toBe(result1.clientOperationId);

    // 3. Verify history preservation: both rows exist
    const identifiers2 = await database.get<AnimalIdentifier>('animal_identifiers').query().fetch();
    expect(identifiers2.length).toBe(2);

    const oldTag = identifiers2.find((i) => i.value === 'TAG-101');
    expect(oldTag).toBeDefined();
    expect(oldTag!.isActive).toBe(false);
    expect(oldTag!.isDeleted).toBe(false);

    const newTag = identifiers2.find((i) => i.value === 'TAG-102');
    expect(newTag).toBeDefined();
    expect(newTag!.isActive).toBe(true);
    expect(newTag!.isDeleted).toBe(false);
  });

  it('preserves full history across multiple replacements (T3.4, Rule 1)', async () => {
    await service.assignIdentifier({ animalId: 'animal-1', value: 'V1' });
    await service.assignIdentifier({ animalId: 'animal-1', value: 'V2' });
    await service.assignIdentifier({ animalId: 'animal-1', value: 'V3' });

    const all = await database.get<AnimalIdentifier>('animal_identifiers').query().fetch();
    expect(all.length).toBe(3);

    const v1 = all.find((i) => i.value === 'V1');
    const v2 = all.find((i) => i.value === 'V2');
    const v3 = all.find((i) => i.value === 'V3');

    expect(v1?.isActive).toBe(false);
    expect(v2?.isActive).toBe(false);
    expect(v3?.isActive).toBe(true);
  });

  it('enqueues assignAnimalIdentifier operations in sync_outbox (T3.1)', async () => {
    await service.assignIdentifier({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-101',
    });

    await service.assignIdentifier({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-102',
      validFrom: '2026-09-08',
    });

    const pending = await outbox.pending();
    expect(pending.length).toBe(2);

    expect(pending[0].operationType).toBe('assignAnimalIdentifier');
    expect(pending[0].payload).toEqual({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-101',
    });

    expect(pending[1].operationType).toBe('assignAnimalIdentifier');
    expect(pending[1].payload).toEqual({
      animalId: 'animal-1',
      type: 'FarmTag',
      value: 'TAG-102',
      validFrom: '2026-09-08',
    });
  });

  it('throws an error if the animal is not found or is deleted', async () => {
    await expect(
      service.assignIdentifier({ animalId: 'non-existent-animal', value: 'TAG-999' }),
    ).rejects.toThrow('No se encontró el animal en este dispositivo.');

    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'animal-deleted';
        row.sex = 'Female';
        row.speciesId = 'species-1';
        row.isDeleted = true;
        row.serverCreatedAt = Date.parse('2026-08-01T00:00:00.000Z');
      });
    });

    await expect(
      service.assignIdentifier({ animalId: 'animal-deleted', value: 'TAG-999' }),
    ).rejects.toThrow('No se encontró el animal en este dispositivo.');
  });
});
