import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { loadBreeds, loadCategories, loadHerd } from '../src/services/herdQueries';

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
    await database.write(async () => {
      await database.get('animals').create((row: any) => {
        row._raw.id = 'animal-1';
        row.sex = 'Female';
        row.speciesId = 'species-bovine';
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
        Object.assign(row, overrides);
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
});
