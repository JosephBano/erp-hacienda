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
 * docs/planes/fase-3-5/sub-planes/3.5a.2-C.md, test 5: the curative path is deeper than
 * vaccination but still bounded — exactly four taps: animal, product, the
 * combined form (one pass, same accounting `MilkingScreen` uses for "type
 * the litres"), confirm.
 */
describe('TreatScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [{ animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null }];
  const products = [{ itemId: 'item-1', name: 'Antibiótico X', unit: 'ml' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-treat-${Math.random()}`,
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

    // Registering a treatment must never depend on reaching the server (Art. 9).
    global.fetch = jest.fn(() => {
      throw new Error('TreatScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('requires notes when no numeric dose is entered', async () => {
    await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    await fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    await fireEvent.press(await screen.findByTestId('treat-continue'));

    expect(await screen.findByText(/las notas son obligatorias/)).toBeTruthy();
    expect(screen.queryByTestId('treat-confirm')).toBeNull();
  });

  it('leaves no dirty state when cancelled mid-form', async () => {
    await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    await fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    await fireEvent.changeText(await screen.findByTestId('treat-dose-input'), '5');

    await fireEvent.press(screen.getByTestId('treat-cancel'));

    // Back at the animal picker, form state wiped.
    expect(await screen.findByTestId('treat-animal-pig-1')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    const doseInput = await screen.findByTestId('treat-dose-input');
    expect(doseInput.props.value).toBe('');
    expect(await outbox.pending()).toHaveLength(0);
  });
});
