import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { AnimalEditService } from '../src/services/animalEditService';

/**
 * The one editable-entity flow (ADR-0008): correcting a breed guessed wrong at
 * registration, or a category that changed. This is what makes the backend's LWW
 * mechanism reachable from an actual phone — before this service existed, no client had
 * any way to trigger an `updateAnimal` push at all.
 */
describe('AnimalEditService', () => {
  let database: Database;
  let outbox: Outbox;
  let service: AnimalEditService;

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-animal-edit-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new AnimalEditService(database);

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

  it('queues an updateAnimal operation with the requested fields', async () => {
    await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-1', categoryId: 'cat-1', birthDate: '2025-01-01' });

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('updateAnimal');
    expect(entry.payload).toMatchObject({
      animalId: 'animal-1',
      breedId: 'breed-1',
      categoryId: 'cat-1',
      birthDate: '2025-01-01',
    });
  });

  /**
   * A device that has never seen this animal edited reports that truthfully — null is a
   * real answer ("I know it was never edited"), not "I don't know". The server's LWW
   * handler treats these very differently (see UpdateAnimalHandler.IsStale).
   */
  it('declares no known baseline for an animal that has never been edited', async () => {
    await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-1' });

    const [entry] = await outbox.pending();
    expect(entry.payload.knownUpdatedAt).toBeNull();
  });

  it('echoes back the locally known edit baseline', async () => {
    const editedAt = Date.parse('2026-08-02T12:00:00.000Z');
    await database.write(async () => {
      const animal = await database.get('animals').find('animal-1');
      await animal.update((row: any) => {
        row.lastEditedAt = editedAt;
      });
    });

    await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-2' });

    const [entry] = await outbox.pending();
    expect(entry.payload.knownUpdatedAt).toBe(new Date(editedAt).toISOString());
  });

  it('allows explicitly clearing a field that was set by mistake', async () => {
    await service.editAnimal({ animalId: 'animal-1', breedId: null });

    const [entry] = await outbox.pending();
    expect(entry.payload.breedId).toBeNull();
  });

  it('refuses to edit an animal this device has never pulled', async () => {
    await expect(service.editAnimal({ animalId: 'unknown-animal', breedId: 'breed-1' })).rejects.toThrow(
      /no (se encontr|existe)/i,
    );

    expect(await outbox.pending()).toHaveLength(0);
  });

  /**
   * The whole point of not applying the edit locally: a losing edit must never be shown
   * as if it had won. The animal's fields stay exactly as this device last pulled them
   * until a subsequent pull brings the server's resolved value.
   */
  it('does not change the local animal record until the server resolves the edit', async () => {
    await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-1' });

    const animal = await database.get('animals').find('animal-1');
    expect((animal as any).breedId).toBeFalsy();
  });

  it('gives each edit its own idempotency key', async () => {
    const first = await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-1' });
    const second = await service.editAnimal({ animalId: 'animal-1', breedId: 'breed-2' });

    expect(first.clientOperationId).not.toBe(second.clientOperationId);
  });
});
