import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * feature-0006 commit 4, criterio 3 / D2 — "confirmar repetidamente mientras se
 * guarda produce una sola operación local y un único resultado visible". Tasks
 * T4.2 is explicit that without this test the commit does not go in.
 *
 * The rows counted here are real: a LokiJS database, the real `MilkingService`
 * and the real `Outbox`. A stub's call count would only prove the screen called
 * something twice; what matters is how many entries an employee has to correct
 * afterwards, and history is immutable (regla dura 1) — a duplicate milking is
 * not a row anyone deletes, it is a correction event somebody has to file about
 * a cow that was milked once.
 *
 * Both taps go inside a single `act`, which is the defect's actual shape: two
 * touches delivered in the same tick. That is what a `busy` state cannot stop —
 * React schedules the flag instead of applying it, so both reads see `false` —
 * and on this screen the window is wider still, because `record` awaits the
 * plausibility read *before* anything would have flipped a flag at all.
 */
describe('MilkingScreen — dos confirmaciones, una entrada (feature-0006 D2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: MilkingService;

  const speciesId = 'species-bovino';
  const candidate = {
    animalId: 'cow-1',
    label: 'La Pinta',
    isWithheld: false,
    speciesIsMilkable: true,
    speciesId,
  };

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-milking-double-tap-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new MilkingService(database);

    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = 'Bovino';
        row.isMilkable = true;
        row.isDeleted = false;
      });
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-1';
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      // A configured range is what puts a real `await` in front of the write on
      // the normal path too — the read the old `busy` flag never covered.
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-milk-1';
        row.speciesId = speciesId;
        row.categoryId = undefined;
        row.magnitude = 'milk_liters';
        row.plausibleMin = 2;
        row.plausibleMax = 40;
        row.absoluteMin = 0.5;
        row.absoluteMax = 2000;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const openForm = async (onRecorded: () => void) => {
    await render(
      <MilkingScreen
        service={service}
        database={database}
        candidates={[candidate]}
        recordedBy="tester@hato"
        onRecorded={onRecorded}
      />,
    );

    const cow = await screen.findByTestId('cow-cow-1');
    await act(async () => {
      fireEvent.press(cow);
    });
  };

  it('encola un solo ordeño cuando se toca "Registrar" dos veces en el mismo gesto', async () => {
    const onRecorded = jest.fn();
    await openForm(onRecorded);

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('liters-input'), '20');
    });

    const confirm = screen.getByTestId('confirm-milking');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });

    expect(await outbox.pending()).toHaveLength(1);
    // T4.3 — a single visible result: one form clear, one "recorded" callback,
    // not two success paths racing over the same screen.
    expect(onRecorded).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('liters-input')).toBeNull();
  });

  it('encola un solo ordeño cuando se toca "Sí, registrar" dos veces en el mismo gesto', async () => {
    const onRecorded = jest.fn();
    await openForm(onRecorded);

    // 1000 L is outside the plausible range and inside the absolute one, so the
    // screen swaps the confirm button for the ADR-0022 dialog.
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('liters-input'), '1000');
    });
    await act(async () => {
      fireEvent.press(screen.getByTestId('confirm-milking'));
    });

    const yes = await screen.findByTestId('milking-confirm-plausibility');
    expect(await outbox.pending()).toHaveLength(0);

    await act(async () => {
      fireEvent.press(yes);
      fireEvent.press(yes);
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].payload).toMatchObject({ totalLiters: 1000, isPlausibilityConfirmed: true });
    expect(onRecorded).toHaveBeenCalledTimes(1);
  });
});
