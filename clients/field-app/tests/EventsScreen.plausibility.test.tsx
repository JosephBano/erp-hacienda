import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { EventsScreen } from '../src/screens/EventsScreen';

/**
 * Wires `evaluatePlausibility` (ADR-0022, 3.5a.6) into the weight ("Pesaje") form —
 * the same evaluator the client's "1000 litros" bug report closed for milking, applied
 * to the other reachable magnitude the plan calls out: `weight_kg`. Runs entirely
 * offline; no test here touches the network.
 */
describe('EventsScreen weight plausibility (ADR-0022)', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const speciesId = 'species-porcino';
  const animal = { animalId: 'pig-1', label: 'Cerdo 01', speciesId };

  const seedRange = async (overrides: Partial<Record<string, number>> = {}) => {
    await database.write(async () => {
      await database.get('plausibility_ranges').create((row: any) => {
        row._raw.id = 'range-weight-1';
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

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-events-plausibility-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de eventos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const goToWeightForm = async () => {
    await render(
      <EventsScreen
        service={service}
        database={database}
        animals={[animal]}
        groups={[]}
        medications={[]}
        initialAnimalId={animal.animalId}
        initialActivity="weight"
      />,
    );
    return screen.findByTestId('weight-input');
  };

  it('passes a weight inside the plausible range without any extra dialog', async () => {
    await seedRange();
    await goToWeightForm();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weight-input'), '80');
    });
    fireEvent.press(screen.getByTestId('confirm-weight'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    expect(screen.queryByTestId('weight-confirm-plausibility')).toBeNull();
    const [entry] = await outbox.pending();
    const payload = JSON.parse((entry.payload as any).payloadJson);
    expect(payload).toMatchObject({ weightKg: 80, isPlausibilityConfirmed: false });
  });

  it('requires explicit confirmation for an improbable weight and records it once confirmed', async () => {
    await seedRange({ absoluteMax: 1000 });
    await goToWeightForm();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weight-input'), '350');
    });
    fireEvent.press(screen.getByTestId('confirm-weight'));

    expect(await screen.findByTestId('weight-confirm-plausibility')).toBeTruthy();
    expect(await outbox.pending()).toHaveLength(0);

    fireEvent.press(screen.getByTestId('weight-confirm-plausibility'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    const [entry] = await outbox.pending();
    const payload = JSON.parse((entry.payload as any).payloadJson);
    expect(payload).toMatchObject({ weightKg: 350, isPlausibilityConfirmed: true });
  });

  it('lets the operator back out of the confirmation instead of forcing the record', async () => {
    await seedRange({ absoluteMax: 1000 });
    await goToWeightForm();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weight-input'), '350');
    });
    fireEvent.press(screen.getByTestId('confirm-weight'));

    fireEvent.press(await screen.findByTestId('weight-cancel-plausibility'));

    await waitFor(() => {
      expect(screen.queryByTestId('weight-confirm-plausibility')).toBeNull();
    });
    expect(await outbox.pending()).toHaveLength(0);
  });

  it('blocks a weight outside the absolute range and never enqueues it', async () => {
    await seedRange({ absoluteMax: 300 });
    await goToWeightForm();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weight-input'), '900');
    });
    fireEvent.press(screen.getByTestId('confirm-weight'));

    expect(await screen.findByText(/fuera de lo posible/i)).toBeTruthy();
    expect(screen.queryByTestId('weight-confirm-plausibility')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });

  /**
   * The species/category has no seeded range yet (a new pig category, a fresh species).
   * ADR-0022 sec.3 is explicit: a missing range must not cost the operator a real
   * datum, so an outlandish weight still passes.
   */
  it('does not block an outlandish weight when no range is configured (fail-open)', async () => {
    await goToWeightForm();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('weight-input'), '900');
    });
    fireEvent.press(screen.getByTestId('confirm-weight'));

    await waitFor(async () => {
      expect(await outbox.pending()).toHaveLength(1);
    });
    expect(screen.queryByTestId('weight-confirm-plausibility')).toBeNull();
  });
});
