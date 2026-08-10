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
 * PLAN-FASE-3-5-PORCINO.md sec.3.5a.7 tasks 1–5: the "Un lote" registration forms.
 * Every test here asserts the operation reaches the local outbox with `global.fetch`
 * wired to throw — the plan's mandatory "registro sin red" proof (PLAN-FASE-3-4 sec.2.1)
 * applied per screen/activity, since a network dependency anywhere in a *registration*
 * path is an Art. 9 violation regardless of how small.
 */
describe('LotEventsScreen (3.5a.7 tasks 1–5)', () => {
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
      dbName: `hato-lotevents-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);
    feedService = new FeedConsumptionService(database);

    global.fetch = jest.fn(() => {
      throw new Error('LotEventsScreen must not call the network to register anything.');
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

  // --- Task 1: pesaje muestral -------------------------------------------------------

  it('registers a sample weighing offline and computes the average from the typed weights', async () => {
    await renderScreen('weighing');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weighing-weights-input'), '45, 48, 50.5, 52');
    });
    fireEvent.press(screen.getByTestId('confirm-weighing'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('recordGroupEvent');
    expect(entry.payload).toMatchObject({ groupId: lot.groupId, eventType: 'Weighing' });
    const payload = JSON.parse((entry.payload as any).payloadJson);

    // The screen rounds to 2 decimals (Math.round), so compare against the rounded
    // value rather than the raw division — 48.875 rounds to 48.88, and asserting
    // closeness to the unrounded figure sits exactly on the 0.005 boundary.
    const expectedAvg = Math.round(((45 + 48 + 50.5 + 52) / 4) * 100) / 100;
    expect(payload.sampleCount).toBe(4);
    expect(payload.avgKg).toBe(expectedAvg);
    expect(payload.minKg).toBe(45);
    expect(payload.maxKg).toBe(52);
    // The average sent is exactly the average this screen computed from the raw
    // weights array carried in the same payload — the app calculates it, the
    // operator never types it (PLAN-FASE-3-5-PORCINO.md sec.3.5a.7 task 1).
    const rawAverage = payload.weights.reduce((sum: number, w: number) => sum + w, 0) / payload.weights.length;
    expect(payload.avgKg).toBe(Math.round(rawAverage * 100) / 100);
  });

  it('rejects an empty weighing before it reaches the outbox', async () => {
    await renderScreen('weighing');

    fireEvent.press(screen.getByTestId('confirm-weighing'));

    expect(await screen.findByText(/al menos un peso/i)).toBeTruthy();
    expect(await outbox.pending()).toHaveLength(0);
  });

  // --- Task 2: baja de lote con causa y cantidad --------------------------------------

  it('registers a lot disposal offline with cause and headcount', async () => {
    await renderScreen('disposal');

    const causeButton = await screen.findByTestId('lot-cause-cause-1');
    await act(async () => {
      fireEvent.press(causeButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('disposal-count-input'), '3');
    });
    fireEvent.press(screen.getByTestId('confirm-disposal-lot'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({
      groupId: lot.groupId,
      eventType: 'Disposal',
      affectedCount: 3,
      causeId: 'cause-1',
    });
  });

  it('does not enqueue a disposal without a positive headcount', async () => {
    await renderScreen('disposal');

    const causeButton = await screen.findByTestId('lot-cause-cause-1');
    await act(async () => {
      fireEvent.press(causeButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('disposal-count-input'), '0');
    });
    fireEvent.press(screen.getByTestId('confirm-disposal-lot'));

    expect(await screen.findByText(/mayor que cero/i)).toBeTruthy();
    expect(await outbox.pending()).toHaveLength(0);
  });

  // --- Task 3: vacunación / tratamiento de lote completo ------------------------------

  it('registers a lot vaccination offline with the treated headcount', async () => {
    await renderScreen('vaccination');

    const medicationButton = await screen.findByTestId('lot-medication-med-1');
    await act(async () => {
      fireEvent.press(medicationButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('lot-dose-input'), '2ml');
      fireEvent.changeText(screen.getByTestId('lot-headcount-input'), '42');
    });
    fireEvent.press(screen.getByTestId('confirm-lot-treatment'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({
      groupId: lot.groupId,
      eventType: 'Vaccination',
      affectedCount: 42,
    });
    const payload = JSON.parse((entry.payload as any).payloadJson);
    expect(payload).toMatchObject({ medicationId: 'med-1', dose: '2ml' });
  });

  it('registers a lot treatment offline as a distinct event type from vaccination', async () => {
    await renderScreen('treatment');

    const medicationButton = await screen.findByTestId('lot-medication-med-1');
    await act(async () => {
      fireEvent.press(medicationButton);
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('lot-headcount-input'), '10');
    });
    fireEvent.press(screen.getByTestId('confirm-lot-treatment'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ eventType: 'Treatment', affectedCount: 10 });
  });

  // --- Task 4: diagnóstico grupal ("hay uno enfermo"), sin identificar cuál ----------

  it('registers a group diagnosis offline without naming an animal', async () => {
    await renderScreen('diagnosis');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('diagnosis-count-input'), '1');
      fireEvent.changeText(screen.getByTestId('diagnosis-condition-input'), 'Cojera');
    });
    fireEvent.press(screen.getByTestId('confirm-diagnosis'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    expect(entry.payload).toMatchObject({ groupId: lot.groupId, eventType: 'Diagnosis', affectedCount: 1 });
    // No animalId anywhere in the payload — the screen never offers an animal picker
    // for this activity (ADR-0015: "no sé cuál").
    expect(entry.payload).not.toHaveProperty('animalId');
    const payload = JSON.parse((entry.payload as any).payloadJson);
    expect(payload.condition).toBe('Cojera');
  });

  // --- Task 5: consumo de alimento del lote en sacos ----------------------------------

  it('registers a feed consumption offline through the inventory operation, not an AnimalEvent', async () => {
    await renderScreen('feed');

    // Exactly one feed item is auto-selected (common pilot case), so the picker step
    // costs zero taps.
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('feed-quantity-input'), '3');
      fireEvent.changeText(screen.getByTestId('feed-unit-input'), 'saco40kg');
    });
    fireEvent.press(screen.getByTestId('confirm-feed'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('recordFeedConsumption');
    expect(entry.payload).toMatchObject({
      groupId: lot.groupId,
      inventoryItemId: 'feed-1',
      quantity: 3,
      unit: 'saco40kg',
      recordedBy: 'field-app',
    });
  });

  // --- Plausibility (ADR-0022) wired into the weighing form, same pass/confirm/block ---

  const seedRange = async (overrides: Partial<Record<string, number>> = {}) => {
    await database.write(async () => {
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-lot-weight-1';
        row.speciesId = speciesId;
        row.categoryId = undefined;
        row.magnitude = 'weight_kg';
        row.plausibleMin = overrides.plausibleMin ?? 20;
        row.plausibleMax = overrides.plausibleMax ?? 120;
        row.absoluteMin = overrides.absoluteMin ?? 1;
        row.absoluteMax = overrides.absoluteMax ?? 300;
        row.isActive = true;
        row.isDeleted = false;
      });
    });
  };

  it('requires explicit confirmation when the average weighing is improbable, and records it once confirmed', async () => {
    await seedRange({ absoluteMax: 1000 });
    await renderScreen('weighing');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weighing-weights-input'), '350, 360');
    });
    fireEvent.press(screen.getByTestId('confirm-weighing'));

    expect(await screen.findByTestId('weighing-confirm-plausibility')).toBeTruthy();
    expect(await outbox.pending()).toHaveLength(0);

    fireEvent.press(screen.getByTestId('weighing-confirm-plausibility'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    const payload = JSON.parse((entry.payload as any).payloadJson);
    expect(payload).toMatchObject({ isPlausibilityConfirmed: true });
  });

  it('blocks a weighing average outside the absolute range and never enqueues it', async () => {
    await seedRange({ absoluteMax: 300 });
    await renderScreen('weighing');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weighing-weights-input'), '900, 910');
    });
    fireEvent.press(screen.getByTestId('confirm-weighing'));

    expect(await screen.findByText(/fuera de lo posible/i)).toBeTruthy();
    expect(screen.queryByTestId('weighing-confirm-plausibility')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });
});
