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
 * See tests/MilkingScreen.plausibility.pass.test.tsx for why each scenario has
 * its own file. ADR-0022 sec.2: a value outside the absolute range is
 * physically/biologically impossible for the species and is rejected outright
 * — it never reaches the outbox, confirmed or not.
 */
describe('MilkingScreen plausibility (ADR-0022): impossible value is blocked', () => {
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
      dbName: `hato-milking-plausibility-block-${Math.random()}`,
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
        row.absoluteMax = 500;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('blocks a value outside the absolute range and never enqueues it', async () => {
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

    expect(screen.getByText(/fuera de lo posible/i)).toBeTruthy();
    expect(screen.queryByTestId('milking-confirm-plausibility')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });
});
