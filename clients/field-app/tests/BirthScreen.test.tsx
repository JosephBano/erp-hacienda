import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { BirthService } from '../src/services/birthService';
import { BirthScreen } from '../src/screens/BirthScreen';

/**
 * Smoke-level coverage for the Birth screen. The interaction-heavy paths are intentionally
 * covered by the logic suites against the real WatermelonDB schema; this file exists
 * specifically to lock the empty-state copy that an employee sees on first launch (no
 * dams yet because the herd has not been synced down).
 */
describe('BirthScreen', () => {
  let database: Database;
  let service: BirthService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-birth-screen-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    service = new BirthService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('points the employee to Sync when there are no dams to choose from', async () => {
    await render(<BirthScreen service={service} dams={[]} sires={[]} />);

    expect(await screen.findByTestId('dam-list-empty')).toBeTruthy();
    expect(screen.queryByTestId('dam-anything')).toBeNull();
  });

  it('renders the dam picker when at least one dam is available', async () => {
    const dams = [{ animalId: 'dam-1', label: 'La Pinta' }];
    await render(<BirthScreen service={service} dams={dams} sires={[]} />);

    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();
    expect(screen.queryByTestId('dam-list-empty')).toBeNull();
  });
});

/**
 * Calf management: the screen used to only know how to add. The client reported the exact
 * scenario "agregué cinco, la tercera era otra cosa y no la pude sacar": once a calf was
 * added it stayed. Removing and changing the sex of an already-added calf is now part of
 * the same screen, no modal, no extra step. These tests pin the order-preserving behaviour
 * — removing index 2 of five must leave the other four in their original order.
 */
describe('BirthScreen — calf management', () => {
  let recordBirth: jest.Mock;

  /**
   * A stub service. The DB setup is intentionally skipped: the rule we are locking is the
   * UI's behaviour around the `offspring` array, not what the service does with it, which
   * has its own suite. The "no network" guarantee is kept as a tripwire.
   */
  const buildService = () => {
    recordBirth = jest.fn().mockResolvedValue({
      clientOperationId: 'op-1',
      damId: 'dam-1',
      birthDate: '2026-08-06',
      offspringCount: 0,
    });
    return { recordBirth } as unknown as BirthService;
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const pickDam = async () => {
    await act(async () => {
      fireEvent.press(await screen.findByTestId('dam-dam-1'));
    });
  };

  const tapAdd = async (kind: 'male' | 'female') => {
    await act(async () => {
      fireEvent.press(screen.getByTestId(kind === 'male' ? 'add-male' : 'add-female'));
    });
  };

  const tap = async (testId: string) => {
    await act(async () => {
      fireEvent.press(screen.getByTestId(testId));
    });
  };

  it('lets the employee remove a calf and submits the remaining four in order', async () => {
    await render(<BirthScreen service={buildService()} dams={[{ animalId: 'dam-1', label: 'La Pinta' }]} sires={[]} />);

    await pickDam();
    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('female');
    await tapAdd('female');

    // remove the third calf (the third male) — the four survivors keep their order.
    await tap('remove-offspring-2');

    expect(screen.getByText('Crías: 4')).toBeTruthy();

    await tap('confirm-birth');

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    expect(recordBirth.mock.calls[0][0].offspring).toEqual([
      { sex: 'M' },
      { sex: 'M' },
      { sex: 'F' },
      { sex: 'F' },
    ]);
  });

  it('toggles the sex of a single calf without disturbing the others', async () => {
    await render(<BirthScreen service={buildService()} dams={[{ animalId: 'dam-1', label: 'La Pinta' }]} sires={[]} />);

    await pickDam();
    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('female');

    // swap the first male to a female — the rest stay put.
    await tap('toggle-offspring-0');

    await tap('confirm-birth');

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    expect(recordBirth.mock.calls[0][0].offspring).toEqual([
      { sex: 'F' },
      { sex: 'M' },
      { sex: 'F' },
    ]);
  });

  /**
   * "Cancelar tras quitar no deja estado sucio" (PLAN §3.5a.0 #2): after a remove that
   * got the litter right, cancelling must take the user back to the dam picker with an
   * empty litter — not a half-cleared draft that re-appears on the next visit.
   */
  it('cancel after a remove clears the litter and sends nothing to the server', async () => {
    await render(<BirthScreen service={buildService()} dams={[{ animalId: 'dam-1', label: 'La Pinta' }]} sires={[]} />);

    await pickDam();
    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('female');

    await tap('remove-offspring-1');

    await tap('cancel-birth');

    // back at the dam picker, no leftover calf list, nothing enqueued.
    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();
    expect(screen.queryByTestId('offspring-count')).toBeNull();
    expect(recordBirth).not.toHaveBeenCalled();
  });
});
