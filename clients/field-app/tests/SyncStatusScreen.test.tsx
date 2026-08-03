import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import '@testing-library/react-native/extend-expect';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { SyncEngine } from '../src/services/syncEngine';
import { SyncStatusScreen } from '../src/screens/SyncStatusScreen';
import type { PullResponse, PushResponse, SyncApi } from '../src/services/syncApi';

/**
 * The screen from PLAN-FASE-3-4 §3.C: sync status an employee can act on, and the tray
 * where refused records stay visible instead of disappearing.
 */
describe('SyncStatusScreen', () => {
  let database: Database;
  let outbox: Outbox;

  const api: SyncApi = {
    async push(ops): Promise<PushResponse> {
      return {
        processedCount: ops.length,
        results: ops.map((o) => ({
          clientOperationId: o.clientOperationId,
          status: 'Rejected' as const,
          resultRef: null,
          errorDetails: 'El animal no existe en el sistema.',
        })),
      };
    },
    async pull(): Promise<PullResponse> {
      return { cursor: 'c', hasMore: false, collections: {} };
    },
  };

  beforeEach(() => {
    (global as any).__resetNativeMocks();

    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-status-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
  });

  it('shows how much of the day has not left the phone', async () => {
    await outbox.enqueue('recordMilking', { totalLiters: 5 });
    await outbox.enqueue('recordMilking', { totalLiters: 7 });

    render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} />);

    await waitFor(() => {
      expect(screen.getByTestId('pending-count')).toHaveTextContent('2');
    });
  });

  it('lists a refused record with the reason instead of dropping it', async () => {
    await outbox.enqueue('recordAnimalEvent', { animalId: 'ghost' });

    render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText('El animal no existe en el sistema.')).toBeTruthy();
    });

    expect(await outbox.rejected()).toHaveLength(1);
  });

  /** No signal is a normal state on this farm, not an error to alarm anybody with. */
  it('explains an offline attempt in words the employee can act on', async () => {
    await outbox.enqueue('recordMilking', { totalLiters: 5 });
    (global as any).__setNetworkConnected(false);

    render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText(/sin señal/i)).toBeTruthy();
    });
  });
});
