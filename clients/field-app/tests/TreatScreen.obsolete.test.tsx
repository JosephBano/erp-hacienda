import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { TreatScreen } from '../src/screens/TreatScreen';

/**
 * T6.1, T6.2, T6.6: Revalidation upon confirm and draft preservation in TreatScreen.
 * When sync changes animal fitness (e.g. disposed or deleted) while the form is open,
 * submission is blocked with a clear notice and the operator's input (dose, notes) is preserved.
 */
describe('TreatScreen: obsolete animal handling and draft preservation', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [
    { animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null },
    { animalId: 'pig-2', label: 'Cerdo 02', speciesId: 'species-1', categoryId: null },
  ];
  const products = [{ itemId: 'item-1', name: 'Antibiótico X', unit: 'ml' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-treat-obsolete-${Math.random()}`,
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
      await database.get('treatment_reasons').create((row: any) => {
        row._raw.id = 'reason-curative';
        row.key = 'curative';
        row.labelEs = 'Curativo';
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
    });

    global.fetch = jest.fn(() => {
      throw new Error('TreatScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('informs when selected animal becomes obsolete during entry, disables confirm, and preserves draft (T6.1, T6.2 & T6.6)', async () => {
    const { rerender } = await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
        initialAnimalId="pig-1"
      />,
    );

    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    await fireEvent.changeText(await screen.findByTestId('treat-dose-input'), '5');
    await fireEvent.changeText(await screen.findByTestId('treat-notes-input'), 'Fiebre alta');
    await fireEvent.press(screen.getByTestId('treat-continue'));

    expect(await screen.findByText(/Dosis: 5 ml/)).toBeTruthy();

    // Simulate sync updating animals: pig-1 is no longer in herd (disposed or deleted)
    const updatedAnimals = [
      { animalId: 'pig-2', label: 'Cerdo 02', speciesId: 'species-1', categoryId: null },
    ];
    await rerender(
      <TreatScreen
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
    const confirmButton = screen.getByTestId('treat-confirm');
    expect(confirmButton.props.accessibilityState?.disabled).toBe(true);

    // Draft is preserved: operator can change animal and keep the dose and notes
    await fireEvent.press(screen.getByTestId('change-animal'));
    expect(await screen.findByTestId('treat-animal-pig-2')).toBeTruthy();

    await fireEvent.press(screen.getByTestId('treat-animal-pig-2'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));

    // The typed dose and notes are still there!
    expect(screen.getByTestId('treat-dose-input').props.value).toBe('5');
    expect(screen.getByTestId('treat-notes-input').props.value).toBe('Fiebre alta');
  });
});
