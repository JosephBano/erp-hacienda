import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { TodayScreen } from '../src/screens/TodayScreen';
import type { Outbox } from '../src/services/outbox';
import type { EventService } from '../src/services/eventService';

/**
 * feature-0006 commit 4 (T4.2), spec D2 and criterio 3 — with the twist this screen
 * carries: the guard is per row, not per screen.
 *
 * `TodayScreen` is the one recording screen that does not use `useSingleFlight`. Every
 * entry of the day has its own "Corregir" button, so a screen-wide latch would make
 * cancelling the 06:00 ordeño block cancelling the 06:05 one — two intentions with
 * nothing to do with each other, serialised for no reason. The guard is a `ref` holding
 * the ids in flight, written synchronously before any `await`, which is the hook's
 * principle applied per identifier.
 *
 * So there are two things to prove, and the second matters as much as the first:
 * a double tap on one row acts once, and two different rows still act in parallel.
 *
 * These are stubs rather than a real outbox because the correction paths are not writes
 * this screen owns: `cancelPending` is a status transition inside the outbox and
 * `recordCorrection` belongs to `EventService`. What is under test is how many times the
 * screen calls them.
 */
describe('TodayScreen — per-row double tap (feature-0006 T4.2)', () => {
  const gated = () => {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  };

  const entries = [
    {
      clientOperationId: 'op-1',
      operationType: 'recordMilking',
      occurredAt: '2026-08-06T06:00:00.000Z',
      status: 'pending' as const,
    },
    {
      clientOperationId: 'op-2',
      operationType: 'recordAnimalEvent',
      occurredAt: '2026-08-06T06:05:00.000Z',
      status: 'pending' as const,
    },
  ];

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('TodayScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('cancels one entry when the same row is tapped twice while processing', async () => {
    const gate = gated();
    const cancelPending = jest.fn(async (_clientOperationId: string) => {
      await gate.promise;
      return 'cancelled' as const;
    });
    const outbox = { cancelPending } as unknown as Outbox;

    await render(
      <TodayScreen
        entries={entries}
        outbox={outbox}
        events={{} as EventService}
        onChanged={() => undefined}
      />,
    );

    const correct = await screen.findByTestId('today-row-op-1-correct');
    await act(async () => {
      fireEvent.press(correct);
      fireEvent.press(correct);
    });
    await act(async () => {
      gate.release();
    });

    // One cancellation for one intention: a second one would report "no encontrado"
    // over the first one's result and read as a failure the operator did not cause.
    expect(cancelPending).toHaveBeenCalledTimes(1);
    expect(cancelPending).toHaveBeenCalledWith('op-1');
  });

  it('still lets two different rows be corrected at the same time', async () => {
    const gate = gated();
    const cancelPending = jest.fn(async (_clientOperationId: string) => {
      await gate.promise;
      return 'cancelled' as const;
    });
    const outbox = { cancelPending } as unknown as Outbox;

    await render(
      <TodayScreen
        entries={entries}
        outbox={outbox}
        events={{} as EventService}
        onChanged={() => undefined}
      />,
    );

    const first = await screen.findByTestId('today-row-op-1-correct');
    const second = await screen.findByTestId('today-row-op-2-correct');
    await act(async () => {
      fireEvent.press(first);
      fireEvent.press(second);
    });
    await act(async () => {
      gate.release();
    });

    // The guard is per row. Serialising these two would be a regression of its own:
    // the operator undoing a mistaken ordeño would have to wait for an unrelated row.
    expect(cancelPending).toHaveBeenCalledTimes(2);
    expect(cancelPending.mock.calls.map((call) => call[0])).toEqual(['op-1', 'op-2']);
  });

  it('enqueues one correction when "Enviar corrección" is tapped twice while saving', async () => {
    const gate = gated();
    const recordCorrection = jest.fn(async () => {
      await gate.promise;
      return { clientOperationId: 'new-op' };
    });
    const events = { recordCorrection } as unknown as EventService;
    const synced = [
      {
        clientOperationId: 'op-3',
        operationType: 'recordAnimalEvent',
        occurredAt: '2026-08-06T06:10:00.000Z',
        status: 'synced' as const,
        resultRef: 'event-77',
      },
    ];

    await render(
      <TodayScreen
        entries={synced}
        outbox={{} as Outbox}
        events={events}
        onChanged={() => undefined}
      />,
    );

    const openReason = await screen.findByTestId('today-row-op-3-correct');
    await act(async () => {
      fireEvent.press(openReason);
    });

    const reason = await screen.findByTestId('today-row-op-3-reason');
    await act(async () => {
      fireEvent.changeText(reason, 'Eran 11, no 10');
    });

    const submit = await screen.findByTestId('today-row-op-3-submit');
    await act(async () => {
      fireEvent.press(submit);
      fireEvent.press(submit);
    });
    await act(async () => {
      gate.release();
    });

    // A correction event is itself immutable history (regla dura 1): two of them means
    // the office reads two corrections for one typo.
    expect(recordCorrection).toHaveBeenCalledTimes(1);
  });
});
