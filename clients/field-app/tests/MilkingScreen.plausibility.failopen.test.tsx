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
 * See tests/MilkingScreen.plausibility.pass.test.tsx for why each scenario has
 * its own file.
 *
 * This is the exact bug the client reported: 1000 L for one cow, with no
 * range configured for the species/category yet. ADR-0022 sec.3 is
 * deliberate about this: a missing range must not block a real datum, so the
 * value passes without friction — the difference with the "block" scenario
 * is entirely whether a range exists at all.
 */
describe('MilkingScreen plausibility (ADR-0022): no range configured is fail-open', () => {
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
      dbName: `hato-milking-plausibility-failopen-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new MilkingService(database);

    // Note: no plausibility_ranges row is seeded here, on purpose.
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
    });

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('does not block an outlandish value when no range is configured (fail-open)', async () => {
    await render(
      <MilkingScreen service={service} database={database} candidates={[candidate]} recordedBy="tester@hato" />,
    );

    const cowButton = await screen.findByTestId('cow-cow-1');
    await act(async () => {
      fireEvent.press(cowButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('liters-input'), '1000');
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
    expect(entry.payload).toMatchObject({ totalLiters: 1000 });
  });
});
