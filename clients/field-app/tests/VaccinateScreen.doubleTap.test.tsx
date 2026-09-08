import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { VaccinateScreen } from '../src/screens/VaccinateScreen';

/**
 * feature-0006 commit 4, criterio 3 / D2 — T4.2. Vaccination has no
 * plausibility dialog (the dose is a fixed one-per-head, never typed), so it
 * has exactly one writing path and the test has exactly one shape: two taps in
 * the same tick, one row in the outbox.
 *
 * The count comes from a real LokiJS outbox rather than a spy, because the unit
 * that matters is the entry an employee would have to correct: a second
 * vaccination on an animal that got one dose is a correction event, not a row
 * anybody deletes (regla dura 1).
 */
describe('VaccinateScreen — dos confirmaciones, una entrada (feature-0006 D2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [{ animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null }];
  const products = [{ itemId: 'item-1', name: 'Triple porcina', unit: 'dosis' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-vaccinate-double-tap-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);

    await database.write(async () => {
      await database.get('administration_routes').create((row: any) => {
        row._raw.id = 'route-im';
        row.key = 'im';
        row.labelEs = 'Intramuscular';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('dose_kinds').create((row: any) => {
        row._raw.id = 'dose-per-head';
        row.key = 'per_head';
        row.labelEs = 'Por cabeza';
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('VaccinateScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('encola una sola vacunación cuando se toca "Confirmar" dos veces en el mismo gesto', async () => {
    const onRecorded = jest.fn();
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onRecorded={onRecorded}
        onCancel={() => undefined}
      />,
    );

    await act(async () => {
      fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    });
    await act(async () => {
      fireEvent.press(await screen.findByTestId('vaccinate-product-item-1'));
    });

    const confirm = await screen.findByTestId('vaccinate-confirm');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });

    expect(await outbox.pending()).toHaveLength(1);
    // T4.3 — one visible result: reset() runs once and the screen is back at
    // the animal picker, not showing two successes.
    expect(onRecorded).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('vaccinate-confirm')).toBeNull();
  });
});
