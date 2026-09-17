import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { EventsScreen } from '../src/screens/EventsScreen';

/**
 * feature-0006 commit 4 (T4.2), spec D2 and criterio 3: "confirmar repetidamente
 * mientras se guarda produce una sola operación local y un único resultado visible".
 *
 * The counting is done on real outbox rows, through the real `EventService` against a
 * real LokiJS database, because that is the thing the employee pays for: a second row
 * is a second `AnimalEvent` at the office, and history is immutable (regla dura 1), so
 * undoing it costs a correction event about a weighing that happened once.
 *
 * `recordWeight` is the worst case on this screen and the reason the latch cannot be a
 * `useState`: it awaits the local plausibility check *before* anything is enqueued, so
 * even a `setBusy(true)` written on the first line would not have rendered yet when the
 * second tap arrives.
 *
 * Both taps go inside one `await act`, with the assertions after it: that is what a
 * double tap is — two presses delivered before React has re-rendered anything — and it
 * also keeps the async tail of the first press out of a second, overlapping act scope.
 */
describe('EventsScreen — double tap (feature-0006 T4.2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [
    { animalId: 'cow-1', label: 'Vaca 01', speciesId: 'species-1', categoryId: null },
  ];
  const groups = [{ groupId: 'lot-1', label: 'Lote 1' }];
  const mortalityCauses = [{ causeId: 'cause-1', name: 'Enfermedad' }];

  /** A save the test decides when to finish, standing in for a slow local write. */
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
      dbName: `hato-events-doubletap-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);

    // Recording an event must never depend on reaching the server (regla dura 10).
    global.fetch = jest.fn(() => {
      throw new Error('EventsScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('writes one weighing when "Registrar pesaje" is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordWeight.bind(service);
    // Real service, real outbox row — only slowed down, so "mientras se guarda" is a
    // window the second tap can actually land in instead of a timing accident.
    service.recordWeight = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await render(
      <EventsScreen
        service={service}
        database={database}
        animals={animals}
        groups={groups}
        initialAnimalId="cow-1"
        initialActivity="weight"
      />,
    );

    const weightInput = await screen.findByTestId('weight-input');
    await act(async () => {
      fireEvent.changeText(weightInput, '420');
    });

    const confirm = await screen.findByTestId('confirm-weight');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordAnimalEvent');
  });

  it('writes one disposal when "Registrar baja" is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordDisposal.bind(service);
    service.recordDisposal = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await render(
      <EventsScreen
        service={service}
        database={database}
        animals={animals}
        groups={groups}
        mortalityCauses={mortalityCauses}
        initialAnimalId="cow-1"
        initialActivity="disposal"
      />,
    );

    const cause = await screen.findByTestId('cause-cause-1');
    await act(async () => {
      fireEvent.press(cause);
    });

    const confirm = await screen.findByTestId('confirm-disposal');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    expect(await outbox.pending()).toHaveLength(1);
  });

  it('writes one move when the same lot button is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordGroupMove.bind(service);
    service.recordGroupMove = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await render(
      <EventsScreen
        service={service}
        database={database}
        animals={animals}
        groups={groups}
        initialAnimalId="cow-1"
        initialActivity="move"
      />,
    );

    const confirm = await screen.findByTestId('group-lot-1');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    expect(await outbox.pending()).toHaveLength(1);
  });
});
