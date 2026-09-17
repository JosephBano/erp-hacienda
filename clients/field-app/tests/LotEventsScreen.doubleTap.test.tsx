import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { FeedConsumptionService } from '../src/services/feedConsumptionService';
import { LotEventsScreen } from '../src/screens/LotEventsScreen';

/**
 * feature-0006 commit 4 (T4.2), spec D2 and criterio 3.
 *
 * A duplicated lot event is worse than a duplicated individual one: a second
 * "Pesaje muestral" or a second "Consumo de alimento" moves numbers that get
 * prorrateados over the whole lot, so the correction has to undo an average and a
 * cost, not one row. Counted on real outbox rows through the real services.
 *
 * The weighing path is the one that proves the point: it awaits the local plausibility
 * check before it ever reaches the enqueue, so a `busy` flag set on the first line of
 * the handler still would not have rendered when the second tap lands.
 */
describe('LotEventsScreen — double tap (feature-0006 T4.2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;
  let feedService: FeedConsumptionService;

  const lots = [{ groupId: 'lot-1', label: 'Lote destete', speciesId: 'species-1' }];
  const feedItems = [{ itemId: 'item-1', name: 'Balanceado', unit: 'kg' }];
  const medications = [{ itemId: 'med-1', name: 'Antibiótico X' }];
  const mortalityCauses = [{ causeId: 'cause-1', name: 'Enfermedad' }];

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
      dbName: `hato-lot-doubletap-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);
    feedService = new FeedConsumptionService(database);

    global.fetch = jest.fn(() => {
      throw new Error('LotEventsScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const renderScreen = (activity: 'weighing' | 'feed' | 'diagnosis') =>
    render(
      <LotEventsScreen
        service={service}
        feedService={feedService}
        database={database}
        lots={lots}
        feedItems={feedItems}
        medications={medications}
        mortalityCauses={mortalityCauses}
        groupId="lot-1"
        activity={activity}
        onBack={() => undefined}
      />,
    );

  it('writes one sample weighing when the confirm is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordGroupEvent.bind(service);
    service.recordGroupEvent = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await renderScreen('weighing');

    const weights = await screen.findByTestId('weighing-weights-input');
    await act(async () => {
      fireEvent.changeText(weights, '45.5, 48, 50.2');
    });

    const confirm = await screen.findByTestId('confirm-weighing');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordGroupEvent');
  });

  it('writes one feed consumption when the confirm is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = feedService.recordFeedConsumption.bind(feedService);
    feedService.recordFeedConsumption = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    // A single feed item is pre-selected by the screen, so the form is already open.
    await renderScreen('feed');

    const quantity = await screen.findByTestId('feed-quantity-input');
    await act(async () => {
      fireEvent.changeText(quantity, '40');
    });

    const confirm = await screen.findByTestId('confirm-feed');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });
    await act(async () => {
      gate.release();
    });

    const pending = await outbox.pending();
    expect(pending).toHaveLength(1);
    expect(pending[0].operationType).toBe('recordFeedConsumption');
  });

  it('writes one diagnosis when the confirm is tapped twice while saving', async () => {
    const gate = gated();
    const enqueue = service.recordGroupEvent.bind(service);
    service.recordGroupEvent = async (input) => {
      await gate.promise;
      return enqueue(input);
    };

    await renderScreen('diagnosis');

    const count = await screen.findByTestId('diagnosis-count-input');
    const condition = await screen.findByTestId('diagnosis-condition-input');
    await act(async () => {
      fireEvent.changeText(count, '2');
      fireEvent.changeText(condition, 'Cojera');
    });

    const confirm = await screen.findByTestId('confirm-diagnosis');
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
