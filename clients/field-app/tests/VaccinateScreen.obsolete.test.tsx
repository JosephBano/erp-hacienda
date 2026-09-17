import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { VaccinateScreen } from '../src/screens/VaccinateScreen';

/**
 * T6.1, T6.2, T6.6: Revalidation upon confirm and draft preservation in VaccinateScreen.
 * When sync changes animal fitness (e.g. disposed or deleted) while the form is open,
 * submission is blocked with a clear notice and the operator's input (selected product) is preserved.
 */
describe('VaccinateScreen: obsolete animal handling and draft preservation', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [
    { animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null },
    { animalId: 'pig-2', label: 'Cerdo 02', speciesId: 'species-1', categoryId: null },
  ];
  const products = [{ itemId: 'item-1', name: 'Triple porcina', unit: 'dosis' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-vaccinate-obsolete-${Math.random()}`,
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

  it('informs when selected animal becomes obsolete during entry, disables confirm, and preserves draft (T6.1, T6.2 & T6.6)', async () => {
    const { rerender } = await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
        initialAnimalId="pig-1"
      />,
    );

    await fireEvent.press(await screen.findByTestId('vaccinate-product-item-1'));

    expect(await screen.findByText(/Triple porcina · 1 dosis por cabeza/)).toBeTruthy();

    // Simulate sync updating animals: pig-1 is no longer in herd (disposed or deleted)
    const updatedAnimals = [
      { animalId: 'pig-2', label: 'Cerdo 02', speciesId: 'species-1', categoryId: null },
    ];
    await rerender(
      <VaccinateScreen
        service={service}
        database={database}
        animals={updatedAnimals}
        products={products}
        onCancel={() => undefined}
        initialAnimalId="pig-1"
      />,
    );

    // Warning notice appears
    expect(await screen.findByText(/ya no existe en el sistema/i)).toBeTruthy();

    // Confirm button is disabled
    const confirmButton = screen.getByTestId('vaccinate-confirm');
    expect(confirmButton.props.accessibilityState?.disabled).toBe(true);

    // Draft is preserved: operator can change animal
    await fireEvent.press(screen.getByTestId('change-animal'));
    expect(await screen.findByTestId('vaccinate-animal-pig-2')).toBeTruthy();

    await fireEvent.press(screen.getByTestId('vaccinate-animal-pig-2'));

    // pig-2 is selected and product item-1 is still the selected product, ready to confirm
    expect(await screen.findByText('Cerdo 02')).toBeTruthy();
    expect(screen.queryByText(/ya no existe en el sistema/i)).toBeNull();
  });
});
