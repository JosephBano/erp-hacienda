import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import {
  loadBreeds,
  loadCategories,
  loadHerd,
  loadMilkingCandidates,
  loadActiveHerd,
  loadPregnantDams,
} from '../src/services/herdQueries';

/**
 * Covers what the animal-edit screen needs and what loadHerd did not carry before: the
 * animal's own species/breed/category/birth date, and the catalogs to pick a new breed or
 * category from — filtered to the animal's species, the same way the panel and the sync
 * pull already do.
 */
describe('herdQueries — editing support', () => {
  let database: Database;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-herdqueries-${Math.random()}`,
    });
    database = new Database({ adapter: adapter as never, modelClasses });
  });

  const seedAnimal = async (overrides: Record<string, unknown> = {}) => {
    const { id, ...rest } = overrides;
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = (id as string) || 'animal-1';
        row.sex = 'Female';
        row.speciesId = 'species-bovine';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
        Object.assign(row, rest);
      });
    });
  };

  const seedBreed = async (id: string, speciesId: string, name: string, isDeleted = false) => {
    await database.write(async () => {
      await database.get('breeds').create((row: any) => {
        row._raw.id = id;
        row.speciesId = speciesId;
        row.name = name;
        row.isDeleted = isDeleted;
      });
    });
  };

  const seedCategory = async (id: string, speciesId: string, name: string, isDeleted = false) => {
    await database.write(async () => {
      await database.get('animal_categories').create((row: any) => {
        row._raw.id = id;
        row.speciesId = speciesId;
        row.name = name;
        row.isDeleted = isDeleted;
      });
    });
  };

  it('carries the fields the edit screen needs to show the current state', async () => {
    await seedAnimal({ breedId: 'breed-1', categoryId: 'cat-1', birthDate: '2025-01-01' });

    const [member] = await loadHerd(database);

    expect(member.speciesId).toBe('species-bovine');
    expect(member.breedId).toBe('breed-1');
    expect(member.categoryId).toBe('cat-1');
    expect(member.birthDate).toBe('2025-01-01');
  });

  it('lists breeds restricted to the given species', async () => {
    await seedBreed('breed-1', 'species-bovine', 'Holstein');
    await seedBreed('breed-2', 'species-porcine', 'Duroc');

    const breeds = await loadBreeds(database, 'species-bovine');

    expect(breeds).toEqual([{ breedId: 'breed-1', label: 'Holstein' }]);
  });

  it('excludes a deleted breed', async () => {
    await seedBreed('breed-1', 'species-bovine', 'Holstein', true);

    expect(await loadBreeds(database, 'species-bovine')).toEqual([]);
  });

  it('lists categories restricted to the given species', async () => {
    await seedCategory('cat-1', 'species-bovine', 'Vaca en producción');
    await seedCategory('cat-2', 'species-porcine', 'Cerdo de engorde');

    const categories = await loadCategories(database, 'species-bovine');

    expect(categories).toEqual([{ categoryId: 'cat-1', label: 'Vaca en producción' }]);
  });

  it('formats animal labels with name prioritized when available', async () => {
    await seedAnimal();
    await database.write(async () => {
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-1';
        row.animalId = 'animal-1';
        row.type = 'Name';
        row.value = 'Margarita';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-2';
        row.animalId = 'animal-1';
        row.type = 'FarmTag';
        row.value = 'CRIA-01';
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    const [member] = await loadHerd(database);
    expect(member.label).toBe('Margarita (CRIA-01)');
  });

  it('distinguishes animals sharing UUID prefix using the last characters of their ID (D11, T4.10b)', async () => {
    await seedAnimal({ id: '00000000-0000-5000-8000-111111110000' });
    await seedAnimal({ id: '00000000-0000-5000-8000-222222220000' });

    const herd = await loadHerd(database);
    expect(herd).toHaveLength(2);
    expect(herd[0].label).not.toBe(herd[1].label);
    expect(herd.map((h) => h.label)).toEqual(
      expect.arrayContaining(['Sin arete · 110000', 'Sin arete · 220000']),
    );
  });
});

describe('herdQueries — loadPregnantDams', () => {
  let database: Database;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-pregnant-dams-${Math.random()}`,
    });
    database = new Database({ adapter: adapter as never, modelClasses });
  });

  const seedDam = async (id: string, name?: string, tag?: string) => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = id;
        row.sex = 'Female';
        row.speciesId = 'species-bovine';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      if (name) {
        await database.get('animal_identifiers').create((row: any) => {
          row._raw.id = `id-name-${id}`;
          row.animalId = id;
          row.type = 'Name';
          row.value = name;
          row.isActive = true;
          row.isDeleted = false;
        });
      }
      if (tag) {
        await database.get('animal_identifiers').create((row: any) => {
          row._raw.id = `id-tag-${id}`;
          row.animalId = id;
          row.type = 'FarmTag';
          row.value = tag;
          row.isActive = true;
          row.isDeleted = false;
        });
      }
    });
  };

  const seedSire = async (id: string, name?: string, tag?: string) => {
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = id;
        row.sex = 'Male';
        row.speciesId = 'species-bovine';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      if (name) {
        await database.get('animal_identifiers').create((row: any) => {
          row._raw.id = `id-name-${id}`;
          row.animalId = id;
          row.type = 'Name';
          row.value = name;
          row.isActive = true;
          row.isDeleted = false;
        });
      }
      if (tag) {
        await database.get('animal_identifiers').create((row: any) => {
          row._raw.id = `id-tag-${id}`;
          row.animalId = id;
          row.type = 'FarmTag';
          row.value = tag;
          row.isActive = true;
          row.isDeleted = false;
        });
      }
    });
  };

  const seedPregnancy = async (
    id: string,
    damId: string,
    options: {
      serviceId?: string;
      status?: string;
      expectedBirthDate?: string;
      isDeleted?: boolean;
    } = {},
  ) => {
    await database.write(async () => {
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = id;
        row.damId = damId;
        row.serviceId = options.serviceId;
        row.status = options.status ?? 'Active';
        row.expectedBirthDate = options.expectedBirthDate;
        row.isDeleted = options.isDeleted ?? false;
        row.serverCreatedAt = Date.now();
      });
    });
  };

  const seedBreedingService = async (
    id: string,
    damId: string,
    options: {
      serviceType: string;
      sireAnimalId?: string;
      strawId?: string;
      isDeleted?: boolean;
    },
  ) => {
    await database.write(async () => {
      await database.get('breeding_services').create((row: any) => {
        row._raw.id = id;
        row.damId = damId;
        row.serviceType = options.serviceType;
        row.sireAnimalId = options.sireAnimalId;
        row.strawId = options.strawId;
        row.isDeleted = options.isDeleted ?? false;
        row.serverCreatedAt = Date.now();
      });
    });
  };

  // Spec 6.3 / T4.8 / T4.11 tests
  it('resolves sireLabel for pregnancy without serviceId to "Padre: sin registrar" (Row 1)', async () => {
    await seedDam('dam-1', 'Alfonsina');
    await seedPregnancy('preg-1', 'dam-1', { serviceId: undefined });

    const [dam] = await loadPregnantDams(database);
    expect(dam.animalId).toBe('dam-1');
    expect(dam.label).toBe('Alfonsina');
    expect(dam.sireLabel).toBe('Padre: sin registrar');
    expect(dam.pregnancyId).toBe('preg-1');
  });

  it('resolves sireLabel for ArtificialInsemination service to "Padre: Inseminación artificial" (Row 2)', async () => {
    await seedDam('dam-1', 'Carlota');
    await seedBreedingService('srv-1', 'dam-1', {
      serviceType: 'ArtificialInsemination',
      strawId: 'straw-123',
    });
    await seedPregnancy('preg-1', 'dam-1', { serviceId: 'srv-1' });

    const [dam] = await loadPregnantDams(database);
    expect(dam.sireLabel).toBe('Padre: Inseminación artificial');
  });

  it('resolves sireLabel for Natural service with existing local sire to "Padre: <etiqueta>" (Row 3)', async () => {
    await seedDam('dam-1', 'Luna');
    await seedSire('bull-1', 'Ferdinand', 'TORO-01');
    await seedBreedingService('srv-1', 'dam-1', {
      serviceType: 'Natural',
      sireAnimalId: 'bull-1',
    });
    await seedPregnancy('preg-1', 'dam-1', { serviceId: 'srv-1' });

    const [dam] = await loadPregnantDams(database);
    expect(dam.sireLabel).toBe('Padre: Ferdinand (TORO-01)');
  });

  it('resolves sireLabel for Natural service with absent local sire to "Padre: sin registrar" (Row 4)', async () => {
    await seedDam('dam-1', 'Maya');
    await seedBreedingService('srv-1', 'dam-1', {
      serviceType: 'Natural',
      sireAnimalId: 'bull-non-existent',
    });
    await seedPregnancy('preg-1', 'dam-1', { serviceId: 'srv-1' });

    const [dam] = await loadPregnantDams(database);
    expect(dam.sireLabel).toBe('Padre: sin registrar');
  });

  // T4.12: preñeces Completed y Aborted quedan fuera
  it('excludes pregnancies with status Completed or Aborted, or isDeleted (T4.12)', async () => {
    await seedDam('dam-1', 'Vaca-Active');
    await seedDam('dam-2', 'Vaca-Completed');
    await seedDam('dam-3', 'Vaca-Aborted');
    await seedDam('dam-4', 'Vaca-DeletedPreg');

    await seedPregnancy('preg-1', 'dam-1', { status: 'Active' });
    await seedPregnancy('preg-2', 'dam-2', { status: 'Completed' });
    await seedPregnancy('preg-3', 'dam-3', { status: 'Aborted' });
    await seedPregnancy('preg-4', 'dam-4', { status: 'Active', isDeleted: true });

    const dams = await loadPregnantDams(database);
    expect(dams).toHaveLength(1);
    expect(dams[0].animalId).toBe('dam-1');
  });

  it('excludes pregnancy when dam animal is marked deleted', async () => {
    await seedDam('dam-1', 'Vaca-Deleted');
    await database.write(async () => {
      const animal = await database.get('animals').find('dam-1');
      await animal.update((r: any) => {
        r.isDeleted = true;
      });
    });

    await seedPregnancy('preg-1', 'dam-1', { status: 'Active' });

    const dams = await loadPregnantDams(database);
    expect(dams).toHaveLength(0);
  });

  // T4.13: orden por expectedBirthDate ascendente
  it('sorts pregnant dams by expectedBirthDate ascending (T4.13)', async () => {
    await seedDam('dam-1', 'Vaca-Late');
    await seedDam('dam-2', 'Vaca-Early');
    await seedDam('dam-3', 'Vaca-Mid');
    await seedDam('dam-4', 'Vaca-NoDate');

    await seedPregnancy('preg-1', 'dam-1', { expectedBirthDate: '2026-09-15' });
    await seedPregnancy('preg-2', 'dam-2', { expectedBirthDate: '2026-08-20' });
    await seedPregnancy('preg-3', 'dam-3', { expectedBirthDate: '2026-08-30' });
    await seedPregnancy('preg-4', 'dam-4', { expectedBirthDate: undefined });

    const dams = await loadPregnantDams(database);
    expect(dams.map((d) => d.animalId)).toEqual(['dam-2', 'dam-3', 'dam-1', 'dam-4']);
  });
});

describe('herdQueries — activity candidates vs historical herd (feature-0005, D1, D5)', () => {
  let database: Database;

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-herdqueries-candidates-${Math.random()}`,
    });
    database = new Database({ adapter: adapter as never, modelClasses });

    // Seed species
    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = 'sp-cow';
        row.name = 'Bovino';
        row.isMilkable = true;
        row.isDeleted = false;
      });
      await database.get('species').create((row: any) => {
        row._raw.id = 'sp-pig';
        row.name = 'Porcino';
        row.isMilkable = false;
        row.isDeleted = false;
      });

      // Female cow (active)
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-1';
        row.sex = 'Female';
        row.speciesId = 'sp-cow';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });

      // Male bull (active)
      await database.get('animals').create((row: any) => {
        row._raw.id = 'bull-1';
        row.sex = 'Male';
        row.speciesId = 'sp-cow';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });

      // Disposed cow (disposed on 2026-08-01)
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-disposed';
        row.sex = 'Female';
        row.speciesId = 'sp-cow';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
        row.disposedAt = '2026-08-01T10:00:00Z';
      });

      // Female sow (active, but species not milkable)
      await database.get('animals').create((row: any) => {
        row._raw.id = 'sow-1';
        row.sex = 'Female';
        row.speciesId = 'sp-pig';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });
  });

  it('loadHerd continues to return males and disposed animals for historical record (D1, T2.3)', async () => {
    const herd = await loadHerd(database, '2026-08-10');
    const ids = herd.map((h) => h.animalId).sort();
    expect(ids).toEqual(['bull-1', 'cow-1', 'cow-disposed', 'sow-1']);

    const disposed = herd.find((h) => h.animalId === 'cow-disposed');
    expect(disposed?.disposedAt).toBe('2026-08-01T10:00:00Z');

    const bull = herd.find((h) => h.animalId === 'bull-1');
    expect(bull?.sex).toBe('Male');
  });

  it('loadMilkingCandidates excludes males, disposed animals and non-milkable species (T2.4)', async () => {
    const candidates = await loadMilkingCandidates(database, '2026-08-10');
    const ids = candidates.map((c) => c.animalId);
    // Only cow-1 is a female of a milkable species and not disposed
    expect(ids).toEqual(['cow-1']);
  });

  it('loadActiveHerd returns both sexes but excludes disposed animals (T5.4)', async () => {
    const active = await loadActiveHerd(database, '2026-08-10');
    const ids = active.map((a) => a.animalId).sort();
    expect(ids).toEqual(['bull-1', 'cow-1', 'sow-1']);
  });

  it('loadPregnantDams excludes males and disposed dams (T5.1)', async () => {
    await database.write(async () => {
      // Pregnancy on male (corrupted data)
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = 'preg-male';
        row.damId = 'bull-1';
        row.status = 'Active';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      // Pregnancy on disposed dam
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = 'preg-disp';
        row.damId = 'cow-disposed';
        row.status = 'Active';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      // Pregnancy on valid active female
      await database.get('pregnancies').create((row: any) => {
        row._raw.id = 'preg-valid';
        row.damId = 'cow-1';
        row.status = 'Active';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    const dams = await loadPregnantDams(database);
    expect(dams.map((d) => d.animalId)).toEqual(['cow-1']);
  });
});

describe('herdQueries — groupName and active identifiers (feature-0007 Commit 1)', () => {
  let database: Database;

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-herdqueries-feature-0007-${Math.random()}`,
    });
    database = new Database({ adapter: adapter as never, modelClasses });

    await database.write(async () => {
      // Species
      await database.get('species').create((row: any) => {
        row._raw.id = 'sp-cow';
        row.name = 'Bovino';
        row.isMilkable = true;
        row.isDeleted = false;
      });

      // Animal 1: tagged cow in a group
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-tagged';
        row.sex = 'Female';
        row.speciesId = 'sp-cow';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });

      // Animal 2: untagged cow without group
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-untagged';
        row.sex = 'Female';
        row.speciesId = 'sp-cow';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });

      // Identifiers for Animal 1: Name, FarmTag, and Official SIFAE tag
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-name-1';
        row.animalId = 'cow-tagged';
        row.type = 'Name';
        row.value = 'Margarita';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-tag-1';
        row.animalId = 'cow-tagged';
        row.type = 'FarmTag';
        row.value = 'CRIA-01';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-tag-official';
        row.animalId = 'cow-tagged';
        row.type = 'Official';
        row.value = 'SIFAE-12345';
        row.isActive = true;
        row.isDeleted = false;
      });

      // Inactive identifier should be excluded
      await database.get('animal_identifiers').create((row: any) => {
        row._raw.id = 'id-tag-old';
        row.animalId = 'cow-tagged';
        row.type = 'FarmTag';
        row.value = 'OLD-TAG';
        row.isActive = false;
        row.isDeleted = false;
      });

      // Groups
      await database.get('animal_groups').create((row: any) => {
        row._raw.id = 'group-1';
        row.name = 'Lote Producción';
        row.trackingMode = 'Individual';
        row.isActive = true;
        row.isDeleted = false;
      });

      // Group membership for Animal 1
      await database.get('group_memberships').create((row: any) => {
        row._raw.id = 'mem-1';
        row.animalId = 'cow-tagged';
        row.groupId = 'group-1';
        row.joinedAt = '2026-01-01';
        row.isActive = true;
        row.isDeleted = false;
      });
    });
  });

  it('associates animal with its active groupName and loads all active identifiers (T1.1, T1.3)', async () => {
    const herd = await loadHerd(database);

    const tagged = herd.find((h) => h.animalId === 'cow-tagged');
    expect(tagged).toBeDefined();
    expect(tagged?.groupName).toBe('Lote Producción');
    expect(tagged?.name).toBe('Margarita');
    expect(tagged?.tag).toBe('CRIA-01');
    expect(tagged?.hasPendingTag).toBe(false);
    expect(tagged?.activeIdentifiers).toHaveLength(3);
    expect(tagged?.activeIdentifiers).toEqual(
      expect.arrayContaining([
        { type: 'Name', value: 'Margarita' },
        { type: 'FarmTag', value: 'CRIA-01' },
        { type: 'Official', value: 'SIFAE-12345' },
      ]),
    );
    // Inactive identifier was excluded
    expect(tagged?.activeIdentifiers.some((id) => id.value === 'OLD-TAG')).toBe(false);

    const untagged = herd.find((h) => h.animalId === 'cow-untagged');
    expect(untagged).toBeDefined();
    expect(untagged?.groupName).toBeUndefined();
    expect(untagged?.tag).toBeUndefined();
    expect(untagged?.name).toBeUndefined();
    expect(untagged?.hasPendingTag).toBe(true);
    expect(untagged?.activeIdentifiers).toEqual([]);
  });

  it('preserves groupName, tag, activeIdentifiers, and hasPendingTag on loadActiveHerd and loadMilkingCandidates', async () => {
    const activeHerd = await loadActiveHerd(database);
    const taggedActive = activeHerd.find((h) => h.animalId === 'cow-tagged');
    expect(taggedActive?.groupName).toBe('Lote Producción');
    expect(taggedActive?.tag).toBe('CRIA-01');
    expect(taggedActive?.hasPendingTag).toBe(false);
    expect(taggedActive?.activeIdentifiers).toHaveLength(3);

    const candidates = await loadMilkingCandidates(database);
    const taggedCandidate = candidates.find((h) => h.animalId === 'cow-tagged');
    expect(taggedCandidate?.groupName).toBe('Lote Producción');
    expect(taggedCandidate?.tag).toBe('CRIA-01');
    expect(taggedCandidate?.hasPendingTag).toBe(false);
    expect(taggedCandidate?.activeIdentifiers).toHaveLength(3);
  });
});
