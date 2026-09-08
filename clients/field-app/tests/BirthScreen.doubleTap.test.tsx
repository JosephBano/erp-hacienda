import React, { useState } from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { BirthService } from '../src/services/birthService';
import { BirthScreen } from '../src/screens/BirthScreen';
import type { PregnantDam } from '../src/services/herdQueries';

/**
 * feature-0006 commit 4, T4.2 and T4.5.
 *
 * A birth is the most expensive duplicate the app can produce: one tap too many used to
 * enqueue a second `recordBirth` for the same pregnancy, with its own calves. Those
 * calves become animals at the office, and history is immutable (regla dura 1) — undoing
 * them is a correction event per animal that was never born.
 *
 * T4.5 is the second half and it lives on this screen because this is the one that
 * navigates away on success: the wizard is unmounted while the save is still settling.
 * The transition must neither leave the latch shut nor warn about state written into a
 * torn-down tree.
 */
const dam: PregnantDam = {
  animalId: 'dam-1',
  label: 'La Pinta',
  pregnancyId: 'preg-1',
  sireLabel: 'Padre: Inseminación artificial',
  expectedBirthDate: '2026-09-01',
};

describe('BirthScreen — double tap (feature-0006 T4.2/T4.5)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: BirthService;

  const gated = () => {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  };

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-birth-doubletap-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new BirthService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  /** Walks the wizard to step 4 with one calf, which is the shortest real parto. */
  const walkToReview = async () => {
    const damButton = await screen.findByTestId('dam-dam-1');
    await act(async () => {
      fireEvent.press(damButton);
    });

    const next2 = await screen.findByTestId('next-step-2');
    await act(async () => {
      fireEvent.press(next2);
    });

    const addFemale = await screen.findByTestId('add-female');
    await act(async () => {
      fireEvent.press(addFemale);
    });

    const next3 = await screen.findByTestId('next-step-3');
    await act(async () => {
      fireEvent.press(next3);
    });

    return screen.findByTestId('confirm-birth');
  };

  it('records one birth when "Registrar parto" is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordBirth.bind(service);
    service.recordBirth = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await render(<BirthScreen service={service} dams={[dam]} />);

    const confirm = await walkToReview();
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordBirth');
  });

  /**
   * T4.5 — "una transición se puede interrumpir con navegación sin bloquear ni
   * duplicar". `App.tsx` answers `onRecorded` with `setTab('home')`, so the wizard is
   * gone before the save finishes settling. Here the parent does the same thing.
   *
   * One press, deliberately: the duplicate guarantee is the test above, and mixing the
   * two would leave this one passing for the wrong reason. What it pins is the other
   * half — the operation completes into a torn-down tree without the screen leaving a
   * warning behind or the latch staying shut on the way out.
   */
  it('survives navigating home mid-save without warning or losing the record', async () => {
    const warnings: unknown[][] = [];
    const realError = console.error;
    console.error = (...args: unknown[]) => {
      warnings.push(args);
      realError(...args);
    };

    const gate = gated();
    const enqueue = service.recordBirth.bind(service);
    service.recordBirth = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    function Host() {
      const [showing, setShowing] = useState(true);
      return showing ? (
        <BirthScreen service={service} dams={[dam]} onRecorded={() => setShowing(false)} />
      ) : null;
    }

    try {
      await render(<Host />);

      const confirm = await walkToReview();
      await act(async () => {
        fireEvent.press(confirm);
      });
      await act(async () => {
        gate.release();
      });

      // Gone home, exactly once, with one entry in the outbox.
      expect(screen.queryByTestId('birth-screen')).toBeNull();
      expect(await outbox.pending()).toHaveLength(1);
      const unmountWarnings = warnings.filter((args) =>
        String(args[0]).includes('unmounted component'),
      );
      expect(unmountWarnings).toEqual([]);
    } finally {
      console.error = realError;
    }
  });
});
