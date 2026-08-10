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
 * PLAN-FASE-3-5-PORCINO-3.5a.2-C, test 4: the scheduled path must cost
 * exactly three taps. Route, reason and dose form are all resolved from the
 * local catalog mirrors — none of them cost a tap.
 */
describe('VaccinateScreen', () => {
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
      dbName: `hato-vaccinate-${Math.random()}`,
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
        row._raw.id = 'dose-absolute';
        row.key = 'absolute';
        row.labelEs = 'Absoluta';
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

    // Registering a vaccination must never depend on reaching the server (Art. 9).
    global.fetch = jest.fn(() => {
      throw new Error('VaccinateScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('records a vaccination in three taps with no network', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    // 1 — choose the animal.
    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    // 2 — choose the product. Route/reason/dose form all resolve here.
    fireEvent.press(await screen.findByTestId('vaccinate-product-item-1'));
    // 3 — confirm. `findByTestId` (not `getByTestId`) so the query itself
    // waits for the 'confirm' step's render to land before the button is
    // looked up; the press is then wrapped in act() together with a few
    // microtask turns so the async chain (the outbox write + reset())
    // actually flushes — see the identical comment in TreatScreen.test.tsx.
    const confirmButton = await screen.findByTestId('vaccinate-confirm');
    await act(async () => {
      fireEvent.press(confirmButton);
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(await outbox.pending()).toHaveLength(1);

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('createTreatmentCourse');
    // The payload's field names are the wire contract with
    // CreateTreatmentCourseCommand — see TreatmentCourseInput's doc comment.
    expect(entry.payload).toMatchObject({
      animalId: 'pig-1',
      routeId: 'route-im',
      reason: 'scheduled',
      productId: 'item-1',
      doseKindId: 'dose-per-head',
      doseFactorAmount: 1,
      doseFactorUnit: 'dosis',
      isPlausibilityConfirmed: false,
    });

    // Screen fell back to the animal picker — reset() actually landed.
    expect(screen.queryByTestId('vaccinate-confirm')).toBeNull();
  });

  it('leaves no dirty state when cancelled after picking an animal', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    expect(await screen.findByTestId('vaccinate-product-item-1')).toBeTruthy();

    fireEvent.press(screen.getByTestId('vaccinate-cancel'));

    // Back at the animal picker — nothing pre-selected.
    expect(await screen.findByTestId('vaccinate-animal-pig-1')).toBeTruthy();
    expect(screen.queryByTestId('vaccinate-product-item-1')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });

  it('shows an empty-catalog message instead of a broken picker when there are no products', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={[]}
        onCancel={() => undefined}
      />,
    );

    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    expect(await screen.findByTestId('vaccinate-product-empty')).toBeTruthy();
  });
});
