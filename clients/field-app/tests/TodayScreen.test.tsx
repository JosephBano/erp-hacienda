import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { TodayScreen } from '../src/screens/TodayScreen';

/**
 * The "Lo que registré hoy" subject. Lists the outbox entries that this phone created
 * today so the operator can audit the day before syncing. Each row carries the
 * operation type, the time, and the sync state so "did my morning make it?" gets a
 * concrete answer. Per the macro plan 2.3, the tap action is "revisar / corregir"
 * (the 3.5a.8 corrections flow) — wired here as a callback so this screen does not
 * own the correction logic, only the navigation towards it.
 */
describe('TodayScreen', () => {
  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('TodayScreen must not call the network.');
    }) as unknown as typeof fetch;
  });

  it('shows the empty-state when nothing has been recorded today', async () => {
    await render(<TodayScreen entries={[]} onSelectEntry={() => undefined} />);

    expect(await screen.findByTestId('today-empty')).toBeTruthy();
  });

  it('lists one row per recorded entry, newest first', async () => {
    const entries = [
      { clientOperationId: 'op-2', operationType: 'recordAnimalEvent', occurredAt: '2026-08-06T15:00:00.000Z', status: 'pending' as const },
      { clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: '2026-08-06T05:00:00.000Z', status: 'pending' as const },
    ];
    await render(<TodayScreen entries={entries} onSelectEntry={() => undefined} />);

    // Row-level testIDs only (omit the inner `today-row-{id}-label` and
    // `today-row-{id}-status-...` children, which start with the same prefix).
    const rows = screen
      .getAllByTestId(/^today-row-/)
      .map((node) => node.props.testID)
      .filter((id) => !id.endsWith('-label') && !id.includes('-status-'));
    expect(rows).toEqual(['today-row-op-2', 'today-row-op-1']);
  });

  it('shows the human label for each operation type', async () => {
    const entries = [
      { clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: '2026-08-06T05:00:00.000Z', status: 'pending' as const },
      { clientOperationId: 'op-2', operationType: 'recordBirth', occurredAt: '2026-08-06T07:00:00.000Z', status: 'synced' as const },
    ];
    await render(<TodayScreen entries={entries} onSelectEntry={() => undefined} />);

    expect(await screen.findByText(/Ordeño/)).toBeTruthy();
    expect(screen.getByText(/Parto/)).toBeTruthy();
  });

  it('routes the entry tap to the parent', async () => {
    const onSelectEntry = jest.fn();
    const entries = [
      { clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: '2026-08-06T05:00:00.000Z', status: 'pending' as const },
    ];
    await render(<TodayScreen entries={entries} onSelectEntry={onSelectEntry} />);

    fireEvent.press(await screen.findByTestId('today-row-op-1'));

    expect(onSelectEntry).toHaveBeenCalledWith('op-1');
  });

  it('marks a synced entry differently from a pending one', async () => {
    const entries = [
      { clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: '2026-08-06T05:00:00.000Z', status: 'synced' as const },
      { clientOperationId: 'op-2', operationType: 'recordMilking', occurredAt: '2026-08-06T06:00:00.000Z', status: 'pending' as const },
    ];
    await render(<TodayScreen entries={entries} onSelectEntry={() => undefined} />);

    expect(await screen.findByTestId('today-row-op-1-status-synced')).toBeTruthy();
    expect(screen.getByTestId('today-row-op-2-status-pending')).toBeTruthy();
  });
});
