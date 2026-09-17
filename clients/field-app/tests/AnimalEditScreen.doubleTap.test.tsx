import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { AnimalEditService } from '../src/services/animalEditService';
import { AnimalEditScreen } from '../src/screens/AnimalEditScreen';
import type { HerdMember } from '../src/services/herdQueries';

/**
 * feature-0006 commit 4 (T4.2), spec D2 and criterio 3.
 *
 * This is the only screen whose duplicate is not just a duplicate: an animal edit is an
 * LWW write (ADR-0008), so two queued `updateAnimal` operations race each other through
 * the server's conflict resolution and the employee gets two answers about one change.
 *
 * Counted on real outbox rows through the real `AnimalEditService`.
 */
describe('AnimalEditScreen — double tap (feature-0006 T4.2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: AnimalEditService;

  const animals: HerdMember[] = [
    {
      animalId: 'animal-1',
      label: 'La Pinta',
      sex: 'Female',
      isWithheld: false,
      speciesId: 'species-1',
      speciesIsMilkable: true,
      activeIdentifiers: [],
      historicalIdentifiers: [],
      hasPendingTag: false,
    },
  ];

  const gated = () => {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  };

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-edit-doubletap-${Math.random()}`,
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
    });

    global.fetch = jest.fn(() => {
      throw new Error('AnimalEditScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('queues one edit when "Guardar cambios" is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.editAnimal.bind(service);
    service.editAnimal = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await render(<AnimalEditScreen database={database} service={service} animals={animals} />);

    const row = await screen.findByTestId('edit-animal-animal-1');
    await act(async () => {
      fireEvent.press(row);
    });

    const birthDate = await screen.findByTestId('edit-birthdate');
    await act(async () => {
      fireEvent.changeText(birthDate, '2024-03-01');
    });

    const confirm = await screen.findByTestId('confirm-edit-animal');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('updateAnimal');
  });
});
