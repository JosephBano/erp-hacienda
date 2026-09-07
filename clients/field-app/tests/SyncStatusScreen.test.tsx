import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { SyncEngine } from '../src/services/syncEngine';
import { SyncStatusScreen } from '../src/screens/SyncStatusScreen';
import { ModuleVisibility } from '../src/services/moduleVisibility';
import type { PullResponse, PushResponse, SyncApi } from '../src/services/syncApi';

/**
 * The screen from docs/spec/plan-0001-fase-3/spec.md sec. 3.C: sync status an employee can act on, and the tray
 * where refused records stay visible instead of disappearing.
 */
describe('SyncStatusScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let visibility: ModuleVisibility;

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
    visibility = new ModuleVisibility(database);
  });

  it('shows how much of the day has not left the phone', async () => {
    await outbox.enqueue('recordMilking', { totalLiters: 5 });
    await outbox.enqueue('recordMilking', { totalLiters: 7 });

    await render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} visibility={visibility} />);

    await waitFor(() => {
      expect(screen.getByTestId('pending-count')).toHaveTextContent('2');
    });
  });

  it('lists a refused record with the reason instead of dropping it', async () => {
    await outbox.enqueue('recordAnimalEvent', { animalId: 'ghost' });

    await render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} visibility={visibility} />);

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

    await render(<SyncStatusScreen engine={new SyncEngine(database, api)} outbox={outbox} visibility={visibility} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText(/sin señal/i)).toBeTruthy();
    });
  });

  it('handles redownload with confirmation flow preserving outbox', async () => {
    const entry = await outbox.enqueue('recordMilking', { totalLiters: 8 });

    let resetCalled = false;
    const engine = new SyncEngine(database, api);
    const originalResetMirror = engine.resetMirror.bind(engine);
    engine.resetMirror = async () => {
      resetCalled = true;
      // Before reset
      expect(await outbox.pending()).toHaveLength(1);
      await originalResetMirror();
      // After resetMirror, outbox still has the pending entry intact
      expect(await outbox.pending()).toHaveLength(1);
    };

    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    // Press "Rehacer descarga"
    fireEvent.press(screen.getByTestId('redownload'));

    // Confirmation card should appear with explanation
    await waitFor(() => {
      expect(screen.getByText(/se volverán a descargar los datos/i)).toBeTruthy();
      expect(screen.getByText(/lo que registraste hoy.*se conserva/i)).toBeTruthy();
    });

    // Press confirm
    fireEvent.press(screen.getByTestId('confirm-redownload'));

    await waitFor(() => {
      expect(resetCalled).toBe(true);
    });

    // Outbox was never wiped; the record still exists in the local database
    const allOutbox = await outbox.all();
    expect(allOutbox).toHaveLength(1);
    expect(allOutbox[0].clientOperationId).toBe(entry.clientOperationId);
  });

  it('dismisses redownload confirmation on cancel without resetting', async () => {
    let resetCalled = false;
    const engine = new SyncEngine(database, api);
    engine.resetMirror = async () => {
      resetCalled = true;
    };

    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    fireEvent.press(screen.getByTestId('redownload'));

    await waitFor(() => {
      expect(screen.getByText(/se volverán a descargar los datos/i)).toBeTruthy();
    });

    fireEvent.press(screen.getByTestId('cancel-redownload'));

    await waitFor(() => {
      expect(screen.queryByText(/se volverán a descargar los datos/i)).toBeNull();
    });
    expect(resetCalled).toBe(false);
  });

  it('surfaces a visible warning notice when sync fails due to an unmapped collection', async () => {
    const brokenApi: SyncApi = {
      async push(): Promise<PushResponse> {
        return { processedCount: 0, results: [] };
      },
      async pull(): Promise<PullResponse> {
        return {
          cursor: 'c',
          hasMore: false,
          collections: {
            unmappedMysteryCollection: [{ id: 'mystery-1' }],
          },
        };
      },
    };

    const engine = new SyncEngine(database, brokenApi);
    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText(/no se pudo enviar/i)).toBeTruthy();
    });
  });

  it('displays a warning notice when pull has pending work and never claims everything updated (T3.4)', async () => {
    const pendingApi: SyncApi = {
      async push(): Promise<PushResponse> {
        return { processedCount: 0, results: [] };
      },
      async pull(): Promise<PullResponse> {
        return {
          cursor: 'c-next',
          hasMore: true,
          collections: {},
        };
      },
    };

    const engine = new SyncEngine(database, pendingApi);
    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText(/quedan datos por descargar/i)).toBeTruthy();
      expect(screen.queryByText(/enviados.*recibidos/i)).toBeNull();
    });
  });

  it('informs how to recover from expired session and preserves local records intact (T5.5)', async () => {
    await outbox.enqueue('recordMilking', { totalLiters: 8 });

    const authApi: SyncApi = {
      async push(): Promise<PushResponse> {
        const err = new Error('Unauthorized');
        err.name = 'AuthenticationExpiredError';
        throw err;
      },
      async pull(): Promise<PullResponse> {
        return { cursor: 'c', hasMore: false, collections: {} };
      },
    };

    const engine = new SyncEngine(database, authApi);
    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    fireEvent.press(screen.getByTestId('sync-now'));

    await waitFor(() => {
      expect(screen.getByText(/la sesión caducó.*sus registros locales están a salvo/i)).toBeTruthy();
      expect(screen.getByTestId('pending-count')).toHaveTextContent('1');
    });

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('does not invoke recovery when offline, preserving local catalog and displaying notice (T7.2)', async () => {
    // Populate an animal in mirror table
    await database.write(async () => {
      await database.get('animals').create((record: any) => {
        record._raw.id = 'cow-preserved';
        record.sex = 'Female';
        record.speciesId = 'sp-1';
        record.isDeleted = false;
        record.serverCreatedAt = Date.now();
      });
    });

    let resetCalled = false;
    const engine = new SyncEngine(database, api);
    engine.resetMirror = async () => {
      resetCalled = true;
    };

    // Simulate offline
    (global as any).__setNetworkConnected(false);

    await render(<SyncStatusScreen engine={engine} outbox={outbox} visibility={visibility} />);

    // Request redownload
    fireEvent.press(screen.getByTestId('redownload'));
    await waitFor(() => {
      expect(screen.getByTestId('confirm-redownload')).toBeTruthy();
    });

    // Confirm redownload while offline
    fireEvent.press(screen.getByTestId('confirm-redownload'));

    await waitFor(() => {
      // Offline notice is displayed
      expect(screen.getByText(/sin señal.*lo registrado está guardado/i)).toBeTruthy();
    });

    // Recovery was NOT invoked
    expect(resetCalled).toBe(false);

    // Local catalog was NOT wiped
    expect(await database.get('animals').query().fetchCount()).toBe(1);
  });
});
