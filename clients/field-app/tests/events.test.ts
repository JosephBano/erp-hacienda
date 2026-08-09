import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { MilkingService } from '../src/services/milkingService';

describe('EventService', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-events-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);
  });

  describe('treatments', () => {
    it('queues the treatment as an animal event', async () => {
      await service.recordTreatment({
        animalId: 'cow-1',
        medicationId: 'med-1',
        medicationName: 'Oxitetraciclina',
        dose: '10 ml',
        milkWithdrawalDays: 5,
      });

      const [entry] = await outbox.pending();

      expect(entry.operationType).toBe('recordAnimalEvent');
      expect(entry.payload).toMatchObject({ animalId: 'cow-1', eventType: 'Treatment' });
    });

    it('refuses a treatment with no medication', async () => {
      await expect(
        service.recordTreatment({
          animalId: 'cow-1',
          medicationId: '',
          medicationName: '',
          dose: '10 ml',
        }),
      ).rejects.toThrow(/medicamento/i);
    });

    /**
     * The blocking rule has to bite immediately, offline. The employee treats a cow at
     * dawn and milks her at dusk with no signal in between: if the withdrawal only
     * appeared after a sync, that milk would be recorded as sellable.
     */
    it('applies the withdrawal locally so the same day\'s milking is already blocked', async () => {
      await service.recordTreatment({
        animalId: 'cow-treated',
        medicationId: 'med-1',
        medicationName: 'Oxitetraciclina',
        dose: '10 ml',
        milkWithdrawalDays: 5,
      });

      const milking = new MilkingService(database);
      const status = await milking.withdrawalStatus('cow-treated');

      expect(status.isWithheld).toBe(true);
    });

    it('does not withhold milk for a treatment with no milk withdrawal', async () => {
      await service.recordTreatment({
        animalId: 'cow-vitamins',
        medicationId: 'med-2',
        medicationName: 'Vitamina AD3E',
        dose: '5 ml',
        milkWithdrawalDays: 0,
      });

      const milking = new MilkingService(database);
      expect((await milking.withdrawalStatus('cow-vitamins')).isWithheld).toBe(false);
    });

    it('computes the local withdrawal end from the treatment date', async () => {
      await service.recordTreatment({
        animalId: 'cow-treated',
        medicationId: 'med-1',
        medicationName: 'Oxitetraciclina',
        dose: '10 ml',
        milkWithdrawalDays: 3,
        occurredAt: '2026-05-10T06:00:00.000Z',
      });

      const rows = await database.get('withdrawal_periods').query().fetch();

      expect((rows[0] as any).startsAt).toBe('2026-05-10');
      expect((rows[0] as any).endsAt).toBe('2026-05-13');
    });
  });

  describe('weighings', () => {
    it('queues a weighing', async () => {
      await service.recordWeight({ animalId: 'cow-1', weightKg: 420 });

      const [entry] = await outbox.pending();
      expect(entry.payload).toMatchObject({ eventType: 'Weighing' });
    });

    it('refuses a weight that is zero or negative', async () => {
      await expect(service.recordWeight({ animalId: 'cow-1', weightKg: 0 })).rejects.toThrow(
        /peso/i,
      );
    });
  });

  describe('group events', () => {
    it('queues a group weighing with no affected count required', async () => {
      await service.recordGroupEvent({
        groupId: 'group-a',
        eventType: 'Weighing',
        payload: { sampleCount: 10, avgKg: 45.2, minKg: 38, maxKg: 52 },
      });

      const [entry] = await outbox.pending();

      expect(entry.operationType).toBe('recordGroupEvent');
      expect(entry.payload).toMatchObject({ groupId: 'group-a', eventType: 'Weighing' });
    });

    it('queues a group disposal with its affected count', async () => {
      await service.recordGroupEvent({
        groupId: 'group-a',
        eventType: 'Disposal',
        affectedCount: 4,
        payload: { count: 4, causeId: null },
      });

      const [entry] = await outbox.pending();
      expect(entry.payload).toMatchObject({ groupId: 'group-a', eventType: 'Disposal', affectedCount: 4 });
    });

    it('refuses a group disposal with no affected count', async () => {
      await expect(
        service.recordGroupEvent({ groupId: 'group-a', eventType: 'Disposal', payload: {} }),
      ).rejects.toThrow(/cabezas/i);
    });

    it('refuses a group event with no group', async () => {
      await expect(
        service.recordGroupEvent({ groupId: '', eventType: 'Weighing', payload: {} }),
      ).rejects.toThrow(/lote/i);
    });
  });

  describe('disposals', () => {
    it('queues a disposal with its cause as a top-level field', async () => {
      await service.recordDisposal({ animalId: 'piglet-1', causeId: 'cause-1' });

      const [entry] = await outbox.pending();

      expect(entry.operationType).toBe('recordAnimalEvent');
      expect(entry.payload).toMatchObject({
        animalId: 'piglet-1',
        eventType: 'Disposal',
        causeId: 'cause-1',
      });
    });

    it('refuses a disposal with no cause', async () => {
      await expect(
        service.recordDisposal({ animalId: 'piglet-1', causeId: '' }),
      ).rejects.toThrow(/causa/i);
    });

    it('refuses a disposal with no animal', async () => {
      await expect(
        service.recordDisposal({ animalId: '', causeId: 'cause-1' }),
      ).rejects.toThrow(/animal/i);
    });
  });

  describe('moves', () => {
    it('queues a move as the operation the server understands', async () => {
      await service.recordGroupMove({
        animalId: 'cow-1',
        fromGroupId: 'group-a',
        toGroupId: 'group-b',
      });

      const [entry] = await outbox.pending();

      expect(entry.operationType).toBe('moveAnimal');
      expect(entry.payload).toMatchObject({
        animalId: 'cow-1',
        fromGroupId: 'group-a',
        toGroupId: 'group-b',
      });
    });

    it('allows a first entry into a lot with no previous group', async () => {
      await service.recordGroupMove({ animalId: 'cow-1', toGroupId: 'group-b' });

      const [entry] = await outbox.pending();
      expect(entry.payload.fromGroupId).toBeUndefined();
    });

    it('refuses a move with no destination', async () => {
      await expect(
        service.recordGroupMove({ animalId: 'cow-1', toGroupId: '' }),
      ).rejects.toThrow(/destino/i);
    });
  });

  describe('animal registration', () => {
    /** Art. 3: the phone mints the identity, so events can reference it right away. */
    it('gives the new animal an id the phone chose', async () => {
      const animal = await service.registerAnimal({ speciesId: 'sp-1', sex: 'Female' });

      const [entry] = await outbox.pending();

      expect(entry.payload).toMatchObject({ id: animal.animalId });
      expect(animal.animalId).toMatch(/^[0-9a-f-]{36}$/);
    });

    it('lets an event be recorded against an animal that has not synced yet', async () => {
      const animal = await service.registerAnimal({ speciesId: 'sp-1', sex: 'Female' });

      await service.recordWeight({ animalId: animal.animalId, weightKg: 38 });

      const pending = await outbox.pending();
      expect(pending).toHaveLength(2);
      expect(pending[1].payload).toMatchObject({ animalId: animal.animalId });
    });
  });

  describe('photos', () => {
    /**
     * The photo is kept as a local reference on the event. Uploading it needs the
     * attachments module, which does not exist yet — so the app must not pretend the
     * picture reached the server.
     */
    it('keeps the local photo reference on the queued event', async () => {
      await service.recordTreatment({
        animalId: 'cow-1',
        medicationId: 'med-1',
        medicationName: 'Oxitetraciclina',
        dose: '10 ml',
        photoUri: 'file:///local/photo.jpg',
      });

      const [entry] = await outbox.pending();
      const details = JSON.parse(String(entry.payload.payloadJson));

      expect(details.photoUri).toBe('file:///local/photo.jpg');
      expect(details.photoUploaded).toBe(false);
    });
  });
});
