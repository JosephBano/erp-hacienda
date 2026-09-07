import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { BirthService } from '../src/services/birthService';

/**
 * A birth is the one field flow that creates a new animal offline, and the flow where a
 * mistake is hardest to notice: a calf whose mother was dropped looks perfectly normal
 * until someone asks about its pedigree years later.
 *
 * The previous implementation queued one `createAnimal` per calf plus a separate event.
 * `createAnimal` carries no genealogy fields at all, so the server silently discarded the
 * mother and father and answered "accepted". One operation, routed to the birthing
 * command, is what keeps the calf and its parentage together.
 */
describe('BirthService', () => {
  let database: Database;
  let outbox: Outbox;
  let service: BirthService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-births-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new BirthService(database);
  });

  it('queues the whole birth as one operation so calf and parentage cannot be split', async () => {
    await service.recordBirth({
      damId: 'dam-1',
      offspring: [{ sex: 'F' }],
    });

    const pending = await outbox.pending();

    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordBirth');
  });

  it('sends the dam so the calf arrives with its mother attached', async () => {
    await service.recordBirth({ damId: 'dam-1', offspring: [{ sex: 'F' }] });

    const [entry] = await outbox.pending();

    expect(entry.payload).toMatchObject({ damId: 'dam-1' });
  });

  it('carries a sire recorded as an animal', async () => {
    await service.recordBirth({
      damId: 'dam-1',
      sireAnimalId: 'bull-1',
      offspring: [{ sex: 'M' }],
    });

    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ sireAnimalId: 'bull-1' });
  });

  it('carries a sire recorded as a semen straw', async () => {
    await service.recordBirth({
      damId: 'dam-1',
      sireStrawId: 'straw-1',
      offspring: [{ sex: 'F' }],
    });

    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ sireStrawId: 'straw-1' });
  });

  it('refuses a birth with no mother', async () => {
    await expect(service.recordBirth({ damId: '', offspring: [{ sex: 'F' }] })).rejects.toThrow(
      /madre/i,
    );
  });

  it('refuses a birth with no calves at all', async () => {
    await expect(service.recordBirth({ damId: 'dam-1', offspring: [] })).rejects.toThrow(/cría/i);
  });

  /** The dual father of ADR-0006: an animal or a straw, never both. */
  it('refuses a father that is both an animal and a straw', async () => {
    await expect(
      service.recordBirth({
        damId: 'dam-1',
        sireAnimalId: 'bull-1',
        sireStrawId: 'straw-1',
        offspring: [{ sex: 'F' }],
      }),
    ).rejects.toThrow(/pajuela/i);
  });

  /** Litters come from the data, not from an `if` per species (Art. 8). */
  it('handles a litter of any size in the same single operation', async () => {
    await service.recordBirth({
      damId: 'sow-1',
      offspring: Array.from({ length: 9 }, (_, i) => ({ sex: i % 2 === 0 ? 'F' : ('M' as const) })),
    });

    const pending = await outbox.pending();

    expect(pending).toHaveLength(1);
    expect((pending[0].payload.offspring as unknown[])).toHaveLength(9);
    expect(pending[0].payload).toMatchObject({ bornAlive: 9 });
  });

  it('records stillbirths without inventing live calves', async () => {
    await service.recordBirth({
      damId: 'dam-1',
      offspring: [{ sex: 'F' }],
      bornDead: 2,
    });

    const [entry] = await outbox.pending();

    expect(entry.payload).toMatchObject({ bornAlive: 1, bornDead: 2 });
  });

  /**
   * A double tap on the register button must not produce two births. The guard is the
   * single idempotency key per birth; the server refuses the replay.
   */
  it('gives one birth exactly one idempotency key', async () => {
    const first = await service.recordBirth({ damId: 'dam-1', offspring: [{ sex: 'F' }] });
    const second = await service.recordBirth({ damId: 'dam-2', offspring: [{ sex: 'F' }] });

    expect(first.clientOperationId).not.toBe(second.clientOperationId);
    expect(await outbox.pending()).toHaveLength(2);
  });

  it('carries pregnancyId in the queued operation payload', async () => {
    await service.recordBirth({
      damId: 'dam-1',
      pregnancyId: 'preg-123',
      offspring: [{ sex: 'F' }],
    });

    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({
      damId: 'dam-1',
      pregnancyId: 'preg-123',
    });
  });

  it('refuses a birth when dam is a male animal', async () => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'bull-dam';
        row.sex = 'Male';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    await expect(
      service.recordBirth({ damId: 'bull-dam', offspring: [{ sex: 'F' }] }),
    ).rejects.toThrow(/hembra/i);
  });

  it('refuses a birth when dam was disposed before or on birth date', async () => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'dead-dam';
        row.sex = 'Female';
        row.disposedAt = '2026-05-10T12:00:00.000Z';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    await expect(
      service.recordBirth({
        damId: 'dead-dam',
        birthDate: '2026-05-10',
        offspring: [{ sex: 'F' }],
      }),
    ).rejects.toThrow(/dada de baja/i);
  });

  it('refuses a birth when pregnancy is not active', async () => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'dam-completed';
        row.sex = 'Female';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = 'preg-done';
        row.damId = 'dam-completed';
        row.status = 'Completed';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    await expect(
      service.recordBirth({
        damId: 'dam-completed',
        pregnancyId: 'preg-done',
        offspring: [{ sex: 'F' }],
      }),
    ).rejects.toThrow(/activa/i);
  });

  it('refuses a birth when pregnancy belongs to a different dam', async () => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'dam-actual';
        row.sex = 'Female';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = 'preg-other';
        row.damId = 'other-dam';
        row.status = 'Active';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    await expect(
      service.recordBirth({
        damId: 'dam-actual',
        pregnancyId: 'preg-other',
        offspring: [{ sex: 'F' }],
      }),
    ).rejects.toThrow(/corresponde/i);
  });
});

