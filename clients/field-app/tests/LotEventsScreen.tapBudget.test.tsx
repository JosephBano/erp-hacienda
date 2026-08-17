import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { FeedConsumptionService } from '../src/services/feedConsumptionService';
import { LotEventsScreen } from '../src/screens/LotEventsScreen';

/**
 * `TapBudget` (GLOSSARY.md "Conteo de toques"): the standard docs/spec/plan-0002-fase-3-5/spec.md
 * sec.2.3 fixes is three taps for the normal path of an activity, four when the
 * "confirmar lo improbable" (ADR-0022) case fires. ADR-0021 left the second level of the
 * "lote" branch gated specifically on this being enforced by test, not by hand-count —
 * this file is that test, satisfied for 3.5a.7 tasks 1–5.
 *
 * Budget applies to `fireEvent.press` calls made *inside the terminal activity screen*
 * (`LotEventsScreen`, reached with the lot and the activity already chosen) — the same
 * convention `MilkingScreen`'s "three taps to a record" comment uses: navigation taps to
 * *reach* the screen (Home → "Un lote" → pick lot → pick activity) are counted
 * separately, at the hub/picker level, exactly like reaching "Ordeño" is not counted
 * inside MilkingScreen's own three-tap claim. Typing into a field is not a tap.
 *
 * If any of these ever needs a fourth (or fifth) `fireEvent.press` to reach the outbox,
 * that is the compuerta firing: docs/spec/plan-0002-fase-3-5/spec.md sec.2.3 says fix the flow, not
 * relax the number.
 */
describe('LotEventsScreen TapBudget (ADR-0021 condition 2)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;
  let feedService: FeedConsumptionService;

  const speciesId = 'species-porcino';
  const lot = { groupId: 'lot-1', label: 'Engorde marzo', speciesId };
  const mortalityCauses = [{ causeId: 'cause-1', name: 'Aplastamiento' }];
  const medications = [{ itemId: 'med-1', name: 'Antibiótico X' }];
  const feedItems = [{ itemId: 'feed-1', name: 'Balanceado engorde', unit: 'kg' }];

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-lotevents-tapbudget-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);
    feedService = new FeedConsumptionService(database);

    global.fetch = jest.fn(() => {
      throw new Error('LotEventsScreen must not call the network.');
    }) as unknown as typeof fetch;
  });

  function renderScreen(activity: 'weighing' | 'disposal' | 'vaccination' | 'treatment' | 'diagnosis' | 'feed') {
    return render(
      <LotEventsScreen
        service={service}
        feedService={feedService}
        database={database}
        lots={[lot]}
        feedItems={feedItems}
        medications={medications}
        mortalityCauses={mortalityCauses}
        groupId={lot.groupId}
        activity={activity}
        onBack={() => undefined}
      />,
    );
  }

  /**
   * Counts `fireEvent.press` calls made by `press`, not `fireEvent.press` used elsewhere.
   * Wrapped in `act` (same convention `MilkingScreen`'s tests use for a selection tap
   * immediately followed by a synchronous query) so the state update the tap causes is
   * flushed before the next `screen.getByTestId` call runs.
   */
  function counter() {
    let taps = 0;
    return {
      press: async (el: any) => {
        taps += 1;
        await act(async () => {
          fireEvent.press(el);
        });
      },
      get taps() {
        return taps;
      },
    };
  }

  const NORMAL_BUDGET = 3;
  const RARE_BUDGET = 4;

  it('feed (task 5, "el más frecuente"): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('feed');

    // Single feed item auto-selects (0 taps) — typing is free — submit is the only tap.
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('feed-quantity-input'), '3');
    });
    await press(screen.getByTestId('confirm-feed'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });

  it('pesaje muestral (task 1): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('weighing');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weighing-weights-input'), '45, 48, 50');
    });
    await press(screen.getByTestId('confirm-weighing'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });

  it('pesaje muestral, valor improbable (raro): confirm path is within the 4-tap budget', async () => {
    await database.write(async () => {
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-tapbudget-1';
        row.speciesId = speciesId;
        row.categoryId = undefined;
        row.magnitude = 'weight_kg';
        row.plausibleMin = 20;
        row.plausibleMax = 120;
        row.absoluteMin = 1;
        row.absoluteMax = 1000;
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    const { press, taps } = counter();
    await renderScreen('weighing');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weighing-weights-input'), '350, 360');
    });
    await press(screen.getByTestId('confirm-weighing'));
    await press(await screen.findByTestId('weighing-confirm-plausibility'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(RARE_BUDGET);
  });

  it('baja con causa (task 2): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('disposal');

    await press(await screen.findByTestId('lot-cause-cause-1'));
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('disposal-count-input'), '3');
    });
    await press(screen.getByTestId('confirm-disposal-lot'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });

  it('vacunar el lote (task 3): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('vaccination');

    await press(await screen.findByTestId('lot-medication-med-1'));
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('lot-headcount-input'), '42');
    });
    await press(screen.getByTestId('confirm-lot-treatment'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });

  it('tratar el lote (task 3): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('treatment');

    await press(await screen.findByTestId('lot-medication-med-1'));
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('lot-headcount-input'), '10');
    });
    await press(screen.getByTestId('confirm-lot-treatment'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });

  it('hay uno enfermo (task 4): normal path is within the 3-tap budget', async () => {
    const { press, taps } = counter();
    await renderScreen('diagnosis');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('diagnosis-count-input'), '1');
      fireEvent.changeText(screen.getByTestId('diagnosis-condition-input'), 'Cojera');
    });
    await press(screen.getByTestId('confirm-diagnosis'));

    await waitFor(async () => expect(await outbox.pending()).toHaveLength(1));
    expect(taps).toBeLessThanOrEqual(NORMAL_BUDGET);
  });
});
