import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { TreatScreen } from '../src/screens/TreatScreen';

/**
 * feature-0006 commit 4, criterio 3 / D2 — T4.2. Same guarantee as
 * `MilkingScreen.doubleTap.test.tsx`, on the screen where a duplicate is worst:
 * a treatment carries a withdrawal period, so two courses for one application
 * are two Art. 19 clocks on an animal that received one dose. History is
 * immutable (regla dura 1); the second row is a correction event somebody has
 * to file, not something anybody deletes.
 *
 * Real LokiJS, real `EventService`, real `Outbox` — the assertion counts rows
 * that would actually sync, not calls to a stub. Both taps land inside one
 * `act` because that is the defect: two touches in the same tick, which a
 * `busy` state cannot see, and which `confirm` used to spend an entire
 * plausibility read still accepting.
 */
describe('TreatScreen — dos confirmaciones, una entrada (feature-0006 D2)', () => {
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
      dbName: `hato-treat-double-tap-${Math.random()}`,
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
      // Configured on purpose: it is what makes `confirm` await a local read
      // before enqueuing, which is the window the old flag left open.
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-dose-1';
        row.speciesId = 'species-1';
        row.categoryId = undefined;
        row.magnitude = 'dose_ml';
        row.plausibleMin = 1;
        row.plausibleMax = 10;
        row.absoluteMin = 0.1;
        row.absoluteMax = 500;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('TreatScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const openConfirm = async (dose: string, onRecorded: () => void) => {
    await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onRecorded={onRecorded}
        onCancel={() => undefined}
      />,
    );

    await act(async () => {
      fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
    });
    await act(async () => {
      fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    });
    await act(async () => {
      fireEvent.changeText(await screen.findByTestId('treat-dose-input'), dose);
    });
    await act(async () => {
      fireEvent.press(screen.getByTestId('treat-continue'));
    });
  };

  it('encola un solo tratamiento cuando se toca "Confirmar" dos veces en el mismo gesto', async () => {
    const onRecorded = jest.fn();
    await openConfirm('5', onRecorded);

    const confirm = await screen.findByTestId('treat-confirm');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });

    expect(await outbox.pending()).toHaveLength(1);
    // T4.3 — one visible result: the screen falls back to the animal picker
    // once, not twice.
    expect(onRecorded).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('treat-confirm')).toBeNull();
  });

  it('encola un solo tratamiento cuando se toca "Sí, registrar" dos veces en el mismo gesto', async () => {
    const onRecorded = jest.fn();
    // 50 ml is outside the plausible range and inside the absolute one, so the
    // ADR-0022 dialog stands in for the confirm button.
    await openConfirm('50', onRecorded);

    await act(async () => {
      fireEvent.press(await screen.findByTestId('treat-confirm'));
    });

    const yes = await screen.findByTestId('treat-confirm-plausibility');
    expect(await outbox.pending()).toHaveLength(0);

    await act(async () => {
      fireEvent.press(yes);
      fireEvent.press(yes);
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].payload).toMatchObject({
      animalId: 'pig-1',
      isPlausibilityConfirmed: true,
    });
    expect(onRecorded).toHaveBeenCalledTimes(1);
  });
});
