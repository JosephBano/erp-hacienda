import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { loadBreeds, loadCategories, loadHerd, loadPregnantDams } from '../src/services/herdQueries';

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

