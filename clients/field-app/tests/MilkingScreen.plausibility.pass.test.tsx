import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * The bug the client reported: "la app acepta 1000 litros de una vaca". This wires
 * `evaluatePlausibility` (ADR-0022, 3.5a.6) into the screen the operator actually
 * reaches, so these tests exercise the dialog end to end — not just the evaluator
 * in isolation (already covered by tests/plausibilityService.test.ts).
 *
 * No network anywhere in this suite: the plausibility check is evaluated 100%
 * locally against the `plausibility_ranges` mirror (Art. 9).
 *
 * Each scenario lives in its own file on purpose. A full-submit interaction
 * (press → type → confirm → outbox write) leaves an unawaited async tail
 * (`refreshSummary`, `setBusy(false)`) racing against React 19 + RTL 14's act()
 * boundaries; a second render *in the same file* right after picks up a
 * corrupted tree (`toJSON()` returns null) — the same class of flakiness
 * already documented in tests/MilkingScreen.test.tsx's skipped cases. Splitting
 * by file sidesteps it: each file gets its own fresh render/module state.
 */
describe('MilkingScreen plausibility (ADR-0022): value inside range', () => {
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
      dbName: `hato-milking-plausibility-pass-${Math.random()}`,
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
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-milk-1';
        row.speciesId = speciesId;
        row.categoryId = undefined;
        row.magnitude = 'milk_liters';
        row.plausibleMin = 2;
        row.plausibleMax = 40;
        row.absoluteMin = 0.5;
        row.absoluteMax = 100;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    // Locks Art. 9: nothing on this screen may reach the network, including the
    // plausibility check.
    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('passes a value inside the plausible range without any extra dialog', async () => {
    await render(
      <MilkingScreen service={service} database={database} candidates={[candidate]} recordedBy="tester@hato" />,
    );

    const cowButton = await screen.findByTestId('cow-cow-1');
    await act(async () => {
      fireEvent.press(cowButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('liters-input'), '20');
    });
    await act(async () => {
      fireEvent.press(screen.getByTestId('confirm-milking'));
      await new Promise((resolve) => setTimeout(resolve, 50));
    });

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });

    expect(screen.queryByTestId('milking-confirm-plausibility')).toBeNull();
    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ totalLiters: 20, isPlausibilityConfirmed: false });
  });
});
