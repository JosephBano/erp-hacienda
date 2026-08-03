import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import '@testing-library/react-native/extend-expect';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * The milking screen is the one that has to beat the notebook, so it is tested the way it
 * is used: with no network at all, counting the taps it costs to record one cow.
 */
describe('MilkingScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let service: MilkingService;

  const candidates = [
    { animalId: 'cow-1', label: 'La Pinta', isWithheld: false },
    { animalId: 'cow-2', label: 'La Negra', isWithheld: true, withheldUntil: '2999-12-31' },
  ];

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-ui-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new MilkingService(database);

    // There is no fetch in this test on purpose: nothing on this screen may depend on
    // reaching the server.
    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('records a cow in three taps with no network', async () => {
    render(<MilkingScreen service={service} candidates={candidates} />);

    // 1 — choose the cow.
    fireEvent.press(screen.getByTestId('cow-cow-1'));
    // 2 — type the litres.
    fireEvent.changeText(screen.getByTestId('liters-input'), '12.5');
    // 3 — confirm.
    fireEvent.press(screen.getByTestId('confirm-milking'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('recordMilking');
  });

  it('marks a cow under withdrawal before she is even selected', () => {
    render(<MilkingScreen service={service} candidates={candidates} />);

    expect(screen.getByTestId('cow-cow-2')).toHaveTextContent(/RETIRO/);
  });

  it('shows the running total for the day', async () => {
    render(<MilkingScreen service={service} candidates={candidates} />);

    fireEvent.press(screen.getByTestId('cow-cow-1'));
    fireEvent.changeText(screen.getByTestId('liters-input'), '10');
    fireEvent.press(screen.getByTestId('confirm-milking'));

    await waitFor(() => {
      expect(screen.getByTestId('daily-total')).toHaveTextContent('10 L');
    });
  });

  it('explains the refusal instead of failing silently when the milk is not sellable', async () => {
    await database.write(async () => {
      await database.get('withdrawal_periods').create((row: any) => {
        row._raw.id = 'wd-1';
        row.animalId = 'cow-2';
        row.eventId = 'e-1';
        row.target = 'Milk';
        row.startsAt = '2000-01-01';
        row.endsAt = '2999-12-31';
        row.isDeleted = false;
      });
    });

    render(<MilkingScreen service={service} candidates={candidates} />);

    fireEvent.press(screen.getByTestId('cow-cow-2'));
    fireEvent.changeText(screen.getByTestId('liters-input'), '9');
    fireEvent.press(screen.getByTestId('confirm-milking'));

    await waitFor(() => {
      expect(screen.getByText(/retiro/i)).toBeTruthy();
    });

    expect(await outbox.pending()).toHaveLength(0);
  });
});
