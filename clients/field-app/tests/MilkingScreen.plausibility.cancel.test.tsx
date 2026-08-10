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
 * its own file. The operator must be able to back out of the confirmation
 * dialog instead of being forced to either confirm or lose the value.
 */
describe('MilkingScreen plausibility (ADR-0022): operator can back out of confirmation', () => {
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
      dbName: `hato-milking-plausibility-cancel-${Math.random()}`,
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
        row.absoluteMax = 2000;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('lets the operator back out of the confirmation instead of forcing the record', async () => {
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
      // Flushes evaluatePlausibility's DB read (an unavoidable async gap
      // inside record()) within this single act() scope, so the resulting
      // setPendingConfirmation(value) update commits cleanly instead of
      // racing the test's own act() boundaries (React 19 + RTL 14).
      await new Promise((resolve) => setTimeout(resolve, 50));
    });

    await act(async () => {
      fireEvent.press(screen.getByTestId('milking-cancel-plausibility'));
      await new Promise((resolve) => setTimeout(resolve, 50));
    });

    expect(await screen.findByTestId('confirm-milking')).toBeTruthy();
    expect(screen.queryByTestId('milking-confirm-plausibility')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });
});
