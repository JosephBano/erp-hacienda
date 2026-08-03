import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import '@testing-library/react-native/extend-expect';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { AnimalEditService } from '../src/services/animalEditService';
import { AnimalEditScreen } from '../src/screens/AnimalEditScreen';
import type { HerdMember } from '../src/services/herdQueries';

describe('AnimalEditScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let service: AnimalEditService;
  let animals: HerdMember[];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-edit-screen-${Math.random()}`,
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
        row.serverCreatedAt = Date.now();
      });
      await database.get('breeds').create((row: any) => {
        row._raw.id = 'breed-1';
        row.speciesId = 'species-1';
        row.name = 'Holstein';
        row.isDeleted = false;
      });
    });

    animals = [{ animalId: 'animal-1', label: 'La Pinta', sex: 'Female', isWithheld: false, speciesId: 'species-1' }];
  });

  it('queues a breed change with no network involved', async () => {
    render(<AnimalEditScreen database={database} service={service} animals={animals} />);

    fireEvent.press(screen.getByTestId('edit-animal-animal-1'));

    await waitFor(() => {
      expect(screen.getByTestId('edit-breed-breed-1')).toBeTruthy();
    });

    fireEvent.press(screen.getByTestId('edit-breed-breed-1'));
    fireEvent.press(screen.getByTestId('confirm-edit-animal'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('updateAnimal');
    expect(entry.payload).toMatchObject({ animalId: 'animal-1', breedId: 'breed-1' });
  });

  it('tells the employee the change is queued, not already applied', async () => {
    render(<AnimalEditScreen database={database} service={service} animals={animals} />);

    fireEvent.press(screen.getByTestId('edit-animal-animal-1'));
    await waitFor(() => screen.getByTestId('edit-breed-breed-1'));

    fireEvent.press(screen.getByTestId('edit-breed-breed-1'));
    fireEvent.press(screen.getByTestId('confirm-edit-animal'));

    await waitFor(() => {
      expect(screen.getByText(/en cola/i)).toBeTruthy();
    });
  });

  it('only lists breeds belonging to the selected animal\'s species', async () => {
    await database.write(async () => {
      await database.get('breeds').create((row: any) => {
        row._raw.id = 'breed-other-species';
        row.speciesId = 'species-2';
        row.name = 'Duroc';
        row.isDeleted = false;
      });
    });

    render(<AnimalEditScreen database={database} service={service} animals={animals} />);
    fireEvent.press(screen.getByTestId('edit-animal-animal-1'));

    await waitFor(() => screen.getByTestId('edit-breed-breed-1'));
    expect(screen.queryByTestId('edit-breed-breed-other-species')).toBeNull();
  });
});
