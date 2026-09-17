import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { FeedConsumptionService } from '../src/services/feedConsumptionService';
import {
  loadActiveHerd,
  loadBreeds,
  loadCategories,
  loadGroups,
  loadHerd,
  loadMilkingCandidates,
  loadPregnantDams,
} from '../src/services/herdQueries';

/**
 * Feature-0007 Commit 5: Coexistence of Individual and Headcount lots (T5.1 - T5.8).
 *
 * Rules:
 * - Clear UI distinction between individual and headcount groups using trackingMode (D2, T5.1).
 * - Individual weight, treatment, movement, disposal associate with animal's sovereign UUID (T5.2).
 * - Feed is ONLY registered by group, never on individual animals (T5.3).
 * - Sample weighing is registered to the lot, never inventing individual animal weights (T5.4).
 * - Headcount lot disposal lowers headcount and does not select animals at random (T5.5).
 * - Individual groups and headcount lots coexist without cross-contamination (T5.6).
 * - Filters and candidate selections in herdQueries work for both sexes and configured species (T5.8).
 */
describe('Feature 0007 Commit 5 — Coexistence of Individual and Headcount Lots', () => {
  let database: Database;
  let outbox: Outbox;
  let eventService: EventService;
  let feedService: FeedConsumptionService;

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-coexistence-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    eventService = new EventService(database);
    feedService = new FeedConsumptionService(database);
  });

  describe('T5.1 & T5.6 — Group Coexistence and trackingMode in herdQueries', () => {
    it('loadGroups returns both Individual and Headcount groups with trackingMode preserved', async () => {
      await database.write(async () => {
        await database.get('animal_groups').create((row: any) => {
          row._raw.id = 'grp-ind-1';
          row.name = 'Vacas Lecheras';
          row.trackingMode = 'Individual';
          row.speciesId = 'sp-bovine';
          row.isActive = true;
          row.isDeleted = false;
        });

        await database.get('animal_groups').create((row: any) => {
          row._raw.id = 'grp-hc-1';
          row.name = 'Engorde Lote 1';
          row.trackingMode = 'Headcount';
          row.speciesId = 'sp-bovine';
          row.isActive = true;
          row.isDeleted = false;
        });
      });

      const groups = await loadGroups(database);

      expect(groups).toHaveLength(2);
      const indGroup = groups.find((g) => g.groupId === 'grp-ind-1');
      const hcGroup = groups.find((g) => g.groupId === 'grp-hc-1');

      expect(indGroup).toBeDefined();
      expect(indGroup?.trackingMode).toBe('Individual');
      expect(indGroup?.label).toBe('Vacas Lecheras');

      expect(hcGroup).toBeDefined();
      expect(hcGroup?.trackingMode).toBe('Headcount');
      expect(hcGroup?.label).toBe('Engorde Lote 1');
    });
  });

  describe('T5.3 — Feed registration only by group', () => {
    it('feed is attributed to group via recordFeedConsumption and has no animalId in payload', async () => {
      await database.write(async () => {
        await database.get('inventory_items').create((row: any) => {
          row._raw.id = 'item-feed-1';
          row.name = 'Concentrado Engorde';
          row.category = 'Feed';
          row.unit = 'saco';
          row.isDeleted = false;
        });
      });

      await feedService.recordFeedConsumption({
        groupId: 'grp-hc-1',
        inventoryItemId: 'item-feed-1',
        quantity: 5,
        unit: 'saco',
      });

      const pending = await outbox.pending();
      expect(pending).toHaveLength(1);
      const entry = pending[0];
      expect(entry.operationType).toBe('recordFeedConsumption');
      expect(entry.payload).toMatchObject({
        groupId: 'grp-hc-1',
        inventoryItemId: 'item-feed-1',
        quantity: 5,
        unit: 'saco',
      });
      expect((entry.payload as any).animalId).toBeUndefined();
    });
  });

  describe('T5.4 — Sample weighing never invents individual animal weights', () => {
    it('sample weighing is recorded as a group event with avgKg and weights array, without creating animal events', async () => {
      await database.write(async () => {
        await database.get('animal_groups').create((row: any) => {
          row._raw.id = 'grp-hc-1';
          row.name = 'Engorde Lote 1';
          row.trackingMode = 'Headcount';
          row.isActive = true;
          row.isDeleted = false;
        });
      });

      await eventService.recordGroupEvent({
        groupId: 'grp-hc-1',
        eventType: 'Weighing',
        affectedCount: 3,
        payload: {
          sampleCount: 3,
          avgKg: 420.5,
          minKg: 400,
          maxKg: 440,
          weights: [400, 421.5, 440],
        },
      });

      const pending = await outbox.pending();
      expect(pending).toHaveLength(1);
      const entry = pending[0];
      expect(entry.operationType).toBe('recordGroupEvent');
      expect(entry.payload).toMatchObject({
        groupId: 'grp-hc-1',
        eventType: 'Weighing',
        affectedCount: 3,
      });

      const parsedPayload = JSON.parse((entry.payload as any).payloadJson);
      expect(parsedPayload).toMatchObject({
        sampleCount: 3,
        avgKg: 420.5,
        minKg: 400,
        maxKg: 440,
        weights: [400, 421.5, 440],
      });

      // Ensure no animalId is assigned and no individual animal event is queued
      expect((entry.payload as any).animalId).toBeUndefined();
      const animalEvents = pending.filter((e) => e.operationType === 'recordAnimalEvent');
      expect(animalEvents).toHaveLength(0);
    });
  });

  describe('T5.5 — Headcount lot disposal lowers headcount and does not select animals at random', () => {
    it('headcount disposal enqueues a group event with affectedCount and causeId, leaving individual animals untouched', async () => {
      await database.write(async () => {
        await database.get('animal_groups').create((row: any) => {
          row._raw.id = 'grp-hc-1';
          row.name = 'Engorde Lote 1';
          row.trackingMode = 'Headcount';
          row.isActive = true;
          row.isDeleted = false;
        });

        // Seed 2 animals in database
        await database.get('animals').create((row: any) => {
          row._raw.id = 'animal-1';
          row.sex = 'Male';
          row.speciesId = 'sp-bovine';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });
        await database.get('animals').create((row: any) => {
          row._raw.id = 'animal-2';
          row.sex = 'Male';
          row.speciesId = 'sp-bovine';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });
      });

      await eventService.recordGroupEvent({
        groupId: 'grp-hc-1',
        eventType: 'Disposal',
        affectedCount: 2,
        causeId: 'cause-pneumonia',
        payload: {},
      });

      const pending = await outbox.pending();
      expect(pending).toHaveLength(1);
      const entry = pending[0];
      expect(entry.operationType).toBe('recordGroupEvent');
      expect(entry.payload).toMatchObject({
        groupId: 'grp-hc-1',
        eventType: 'Disposal',
        affectedCount: 2,
        causeId: 'cause-pneumonia',
      });
      expect((entry.payload as any).animalId).toBeUndefined();

      // Animals in the database remain untouched (not marked as disposed at random)
      const herd = await loadHerd(database);
      expect(herd).toHaveLength(2);
      expect(herd.every((a) => !a.disposedAt)).toBe(true);
    });
  });

  describe('T5.2 — Individual activities associate with sovereign UUID', () => {
    it('individual weight, treatment, movement, and disposal associate with animal sovereign UUID', async () => {
      await database.write(async () => {
        await database.get('animals').create((row: any) => {
          row._raw.id = 'animal-uuid-1234';
          row.sex = 'Female';
          row.speciesId = 'sp-bovine';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });
      });

      // 1. Individual Weight
      await eventService.recordWeight({
        animalId: 'animal-uuid-1234',
        weightKg: 450,
      });

      // 2. Individual Treatment
      await eventService.recordTreatment({
        animalId: 'animal-uuid-1234',
        medicationId: 'med-1',
        medicationName: 'Antibiótico',
        dose: '10ml',
      });

      // 3. Individual Movement
      await eventService.recordGroupMove({
        animalId: 'animal-uuid-1234',
        toGroupId: 'grp-pasture-2',
      });

      // 4. Individual Disposal
      await eventService.recordDisposal({
        animalId: 'animal-uuid-1234',
        causeId: 'cause-sale',
        notes: 'Venta',
      });

      const pending = await outbox.pending();
      expect(pending).toHaveLength(4);

      // Verify each operation has the sovereign UUID
      for (const entry of pending) {
        expect((entry.payload as any).animalId).toBe('animal-uuid-1234');
      }

      // Check operation types
      expect(pending.map((e) => e.operationType)).toEqual([
        'recordAnimalEvent',
        'recordAnimalEvent',
        'moveAnimal',
        'recordAnimalEvent',
      ]);
    });
  });

  describe('T5.8 — Filters and candidate selections work for both sexes and configured species', () => {
    beforeEach(async () => {
      await database.write(async () => {
        // Species 1: Bovine (milkable)
        await database.get('species').create((row: any) => {
          row._raw.id = 'sp-bov';
          row.name = 'Bovino';
          row.isMilkable = true;
          row.isDeleted = false;
        });

        // Species 2: Porcine (not milkable)
        await database.get('species').create((row: any) => {
          row._raw.id = 'sp-porc';
          row.name = 'Porcino';
          row.isMilkable = false;
          row.isDeleted = false;
        });

        // Animals across sexes and species:
        // 1. Bovine Female
        await database.get('animals').create((row: any) => {
          row._raw.id = 'bov-female';
          row.sex = 'Female';
          row.speciesId = 'sp-bov';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // 2. Bovine Male
        await database.get('animals').create((row: any) => {
          row._raw.id = 'bov-male';
          row.sex = 'Male';
          row.speciesId = 'sp-bov';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // 3. Porcine Female
        await database.get('animals').create((row: any) => {
          row._raw.id = 'porc-female';
          row.sex = 'Female';
          row.speciesId = 'sp-porc';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // 4. Porcine Male
        await database.get('animals').create((row: any) => {
          row._raw.id = 'porc-male';
          row.sex = 'Male';
          row.speciesId = 'sp-porc';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // 5. Disposed Bovine Female
        await database.get('animals').create((row: any) => {
          row._raw.id = 'bov-female-disposed';
          row.sex = 'Female';
          row.speciesId = 'sp-bov';
          row.disposedAt = '2026-08-01T00:00:00Z';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // Breeds per species
        await database.get('breeds').create((row: any) => {
          row._raw.id = 'breed-holstein';
          row.speciesId = 'sp-bov';
          row.name = 'Holstein';
          row.isDeleted = false;
        });
        await database.get('breeds').create((row: any) => {
          row._raw.id = 'breed-duroc';
          row.speciesId = 'sp-porc';
          row.name = 'Duroc';
          row.isDeleted = false;
        });

        // Categories per species
        await database.get('animal_categories').create((row: any) => {
          row._raw.id = 'cat-vaca-prod';
          row.speciesId = 'sp-bov';
          row.name = 'Vaca en producción';
          row.isDeleted = false;
        });
        await database.get('animal_categories').create((row: any) => {
          row._raw.id = 'cat-cerdo-engorde';
          row.speciesId = 'sp-porc';
          row.name = 'Cerdo de engorde';
          row.isDeleted = false;
        });
      });
    });

    it('loadActiveHerd includes both sexes and all configured species, excluding disposed animals', async () => {
      const active = await loadActiveHerd(database, '2026-08-10');
      const ids = active.map((a) => a.animalId).sort();

      expect(ids).toEqual(['bov-female', 'bov-male', 'porc-female', 'porc-male']);
    });

    it('loadMilkingCandidates selects only females of milkable species', async () => {
      const candidates = await loadMilkingCandidates(database, '2026-08-10');
      const ids = candidates.map((a) => a.animalId);

      // Only bov-female qualifies (porcine is not milkable, males are not milkable, disposed is excluded)
      expect(ids).toEqual(['bov-female']);
    });

    it('loadBreeds and loadCategories return catalogs scoped strictly to configured species', async () => {
      const bovineBreeds = await loadBreeds(database, 'sp-bov');
      expect(bovineBreeds).toEqual([{ breedId: 'breed-holstein', label: 'Holstein' }]);

      const porcineBreeds = await loadBreeds(database, 'sp-porc');
      expect(porcineBreeds).toEqual([{ breedId: 'breed-duroc', label: 'Duroc' }]);

      const bovineCats = await loadCategories(database, 'sp-bov');
      expect(bovineCats).toEqual([{ categoryId: 'cat-vaca-prod', label: 'Vaca en producción' }]);

      const porcineCats = await loadCategories(database, 'sp-porc');
      expect(porcineCats).toEqual([{ categoryId: 'cat-cerdo-engorde', label: 'Cerdo de engorde' }]);
    });

    it('loadPregnantDams restricts candidate dams strictly to living females', async () => {
      await database.write(async () => {
        // Valid pregnancy on active bovine female
        await database.get('pregnancies').create((row: any) => {
          row._raw.id = 'preg-1';
          row.damId = 'bov-female';
          row.status = 'Active';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });

        // Invalid pregnancy assigned to male (should be ignored by query)
        await database.get('pregnancies').create((row: any) => {
          row._raw.id = 'preg-male';
          row.damId = 'bov-male';
          row.status = 'Active';
          row.isDeleted = false;
          row.serverCreatedAt = Date.now();
        });
      });

      const pregnantDams = await loadPregnantDams(database);
      expect(pregnantDams).toHaveLength(1);
      expect(pregnantDams[0].animalId).toBe('bov-female');
      expect(pregnantDams[0].pregnancyId).toBe('preg-1');
    });
  });
});
