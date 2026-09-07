import fs from 'fs';
import path from 'path';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../../database/schema';
import { migrations } from '../../database/migrations';
import { modelClasses } from '../../database/models';
import { Outbox } from '../outbox';
import { MilkingService } from '../milkingService';
import { EventService } from '../eventService';
import { BirthService } from '../birthService';
import { AnimalEditService } from '../animalEditService';
import { FeedConsumptionService } from '../feedConsumptionService';

describe('Push Payload Contracts Generator and Validator', () => {
  const fixturePath = path.resolve(__dirname, '../../../../../docs/contracts/push-payloads.json');

  it('exercises every mobile service and generates docs/contracts/push-payloads.json', async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-contracts-${Math.random()}`,
    });

    const database = new Database({ adapter: adapter as never, modelClasses });
    const outbox = new Outbox(database);

    const cowId = '00000000-0000-0000-0000-000000000001';
    const speciesId = '00000000-0000-0000-0000-000000000099';

    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = 'Bovino';
        row.gestationDays = 283;
        row.isMilkable = true;
        row.isDeleted = false;
      });

      await database.get('animals').create((row: any) => {
        row._raw.id = cowId;
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.parse('2026-08-01T00:00:00.000Z');
      });
    });

    const milkingService = new MilkingService(database);
    const eventService = new EventService(database);
    const birthService = new BirthService(database);
    const animalEditService = new AnimalEditService(database);
    const feedConsumptionService = new FeedConsumptionService(database);

    // 1. recordMilking
    await milkingService.recordIndividualYield(
      cowId,
      'Morning',
      12.5,
      'tester@hato',
      '2026-09-07',
      false,
    );

    // 2. recordAnimalEvent
    await eventService.recordWeight({
      animalId: cowId,
      weightKg: 450,
      notes: 'Pesaje mensual',
      isPlausibilityConfirmed: false,
      occurredAt: '2026-09-07T08:00:00.000Z',
    });

    // 3. createTreatmentCourse
    await eventService.recordTreatmentCourse({
      animalId: cowId,
      startsAt: '2026-09-07T08:00:00.000Z',
      routeId: '00000000-0000-0000-0000-000000000002',
      reason: 'Preventivo',
      productId: '00000000-0000-0000-0000-000000000003',
      doseKindId: '00000000-0000-0000-0000-000000000004',
      doseFactorAmount: 1,
      doseFactorUnit: 'ml',
      notes: 'Dosis preventiva',
      administeredDoseAmount: 10,
      administeredDoseUnit: 'ml',
      applicationNotes: 'Aplicado sin novedad',
      milkWithdrawalDays: 0,
      meatWithdrawalDays: 0,
      isPlausibilityConfirmed: false,
    });

    // 4. recordGroupEvent
    await eventService.recordGroupEvent({
      groupId: '00000000-0000-0000-0000-000000000005',
      eventType: 'Weighing',
      occurredAt: '2026-09-07T08:00:00.000Z',
      cost: 0,
      affectedCount: 15,
      payload: { averageWeightKg: 280 },
    });

    // 5. moveAnimal
    await eventService.recordGroupMove({
      animalId: cowId,
      toGroupId: '00000000-0000-0000-0000-000000000006',
      fromGroupId: '00000000-0000-0000-0000-000000000005',
      movedOn: '2026-09-07',
    });

    // 6. recordCorrection
    await eventService.recordCorrection({
      originalEventId: '00000000-0000-0000-0000-000000000007',
      reason: 'Corrección de peso digitado por error',
      occurredAt: '2026-09-07T08:30:00.000Z',
    });

    // 7. createAnimal
    await eventService.registerAnimal({
      speciesId,
      sex: 'Female',
      birthDate: '2025-01-15',
    });

    // 8. recordBirth
    await birthService.recordBirth({
      damId: cowId,
      birthDate: '2026-09-07',
      difficulty: 'Normal',
      bornDead: 0,
      mummified: 0,
      notes: 'Parto normal',
      offspring: [{ sex: 'Female', farmTag: 'H-101', birthWeightKg: 38 }],
    });

    // 9. updateAnimal
    await animalEditService.editAnimal({
      animalId: cowId,
      breedId: '00000000-0000-0000-0000-000000000008',
      categoryId: '00000000-0000-0000-0000-000000000009',
      birthDate: '2023-01-01',
    });

    // 10. recordFeedConsumption
    await feedConsumptionService.recordFeedConsumption({
      groupId: '00000000-0000-0000-0000-000000000005',
      inventoryItemId: '00000000-0000-0000-0000-000000000010',
      quantity: 25.5,
      consumedAt: '2026-09-07',
      unit: 'kg',
      batchId: '00000000-0000-0000-0000-000000000011',
      notes: 'Alimento concentrado',
    });

    const entries = await outbox.pending();
    expect(entries.length).toBe(10);

    const operations = entries.map((entry) => ({
      operationType: entry.operationType,
      payload: entry.payload,
    }));

    const fixtureContent = {
      _comment:
        'GENERATED FILE - DO NOT EDIT MANUALLY. Generated by running "npm test -- pushPayloadContracts" in clients/field-app. See ADR-0008, feature-0004 D7.',
      generatedAt: '2026-09-07T00:00:00.000Z',
      operations,
    };

    const contractsDir = path.dirname(fixturePath);
    if (!fs.existsSync(contractsDir)) {
      fs.mkdirSync(contractsDir, { recursive: true });
    }

    fs.writeFileSync(fixturePath, JSON.stringify(fixtureContent, null, 2) + '\n', 'utf8');

    expect(fs.existsSync(fixturePath)).toBe(true);
  });

  it('fails if any service enqueues an operationType missing from the contract fixture', () => {
    const servicesDir = path.resolve(__dirname, '..');
    const serviceFiles = fs.readdirSync(servicesDir).filter((f) => f.endsWith('.ts'));
    const enqueuedTypes = new Set<string>();

    for (const file of serviceFiles) {
      const content = fs.readFileSync(path.join(servicesDir, file), 'utf8');
      const matches = content.matchAll(/outbox\.enqueue\(\s*['"]([a-zA-Z0-9_-]+)['"]/g);
      for (const match of matches) {
        enqueuedTypes.add(match[1]);
      }
    }

    expect(fs.existsSync(fixturePath)).toBe(true);
    const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
    const fixtureTypes = new Set(fixture.operations.map((op: any) => op.operationType));

    for (const type of enqueuedTypes) {
      expect(fixtureTypes.has(type)).toBe(true);
    }
    for (const type of fixtureTypes) {
      expect(enqueuedTypes.has(type)).toBe(true);
    }
  });
});
