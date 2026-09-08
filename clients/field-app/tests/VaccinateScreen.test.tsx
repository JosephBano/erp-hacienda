import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { VaccinateScreen } from '../src/screens/VaccinateScreen';

/**
 * docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md, test 4: the scheduled path must cost
 * exactly three taps. Route, reason and dose form are all resolved from the
 * local catalog mirrors — none of them cost a tap.
 */
/**
 * feature-0006 commit 2 — structural helpers, copied from `Screen.test.tsx` on
 * purpose. RNTL measures no layout, so these prove nothing about pixels; what
 * they pin is the structure that makes content unreachable — a second vertical
 * scroller stealing the drag, and a clamped content box capping the screen at
 * one window. The on-device check is `test-e2e.md` (criterion 6).
 */
const countScrollers = (node: unknown): number => {
  if (!node || typeof node !== 'object') return 0;
  const element = node as { type?: string; children?: unknown[] };
  const self = element.type === 'RCTScrollView' || element.type === 'ScrollView' ? 1 : 0;
  return (element.children ?? []).reduce<number>((acc, child) => acc + countScrollers(child), self);
};

const flatten = (style: unknown): Record<string, unknown> =>
  Array.isArray(style)
    ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
    : ((style ?? {}) as Record<string, unknown>);

type RenderedNode = { props?: { style?: unknown; testID?: string }; children?: unknown[] };

const findNode = (node: unknown, testID: string): RenderedNode | null => {
  if (!node || typeof node !== 'object') return null;
  const element = node as RenderedNode;
  if (element.props?.testID === testID) return element;
  for (const child of element.children ?? []) {
    const found = findNode(child, testID);
    if (found) return found;
  }
  return null;
};

const flexValues = (node: unknown): unknown[] => {
  if (!node || typeof node !== 'object') return [];
  const element = node as RenderedNode;
  const own = flatten(element.props?.style).flex;
  return (element.children ?? []).reduce<unknown[]>(
    (acc, child) => acc.concat(flexValues(child)),
    own === undefined ? [] : [own],
  );
};

/**
 * Every `flex` declared *inside* the screen container. A `1` in there is the defect
 * this commit removes: a clamped box inside scrollable content re-bounds the form to
 * one window, so the tail sits below the fold with nothing left to scroll. The walk
 * starts below the container because the scroller and its KeyboardAvoidingView
 * legitimately carry `flex: 1` — they *are* the window.
 */
const clampsInsideScreen = (testID: string, tree: unknown): unknown[] =>
  (findNode(tree, testID)?.children ?? []).reduce<unknown[]>(
    (acc, child) => acc.concat(flexValues(child)),
    [],
  );

describe('VaccinateScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [{ animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null }];
  const products = [{ itemId: 'item-1', name: 'Triple porcina', unit: 'dosis' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-vaccinate-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new EventService(database);

    await database.write(async () => {
      await database.get('administration_routes').create((row: any) => {
        row._raw.id = 'route-im';
        row.key = 'im';
        row.labelEs = 'Intramuscular';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('dose_kinds').create((row: any) => {
        row._raw.id = 'dose-absolute';
        row.key = 'absolute';
        row.labelEs = 'Absoluta';
        row.isActive = true;
        row.isDeleted = false;
      });
      await database.get('dose_kinds').create((row: any) => {
        row._raw.id = 'dose-per-head';
        row.key = 'per_head';
        row.labelEs = 'Por cabeza';
        row.isActive = true;
        row.isDeleted = false;
      });
    });

    // Registering a vaccination must never depend on reaching the server (Art. 9).
    global.fetch = jest.fn(() => {
      throw new Error('VaccinateScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('records a vaccination in three taps with no network', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    // 1 — choose the animal.
    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    // 2 — choose the product. Route/reason/dose form all resolve here.
    fireEvent.press(await screen.findByTestId('vaccinate-product-item-1'));
    // 3 — confirm. `findByTestId` (not `getByTestId`) so the query itself
    // waits for the 'confirm' step's render to land before the button is
    // looked up; the press is then wrapped in act() together with a few
    // microtask turns so the async chain (the outbox write + reset())
    // actually flushes — see the identical comment in TreatScreen.test.tsx.
    const confirmButton = await screen.findByTestId('vaccinate-confirm');
    await act(async () => {
      fireEvent.press(confirmButton);
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(await outbox.pending()).toHaveLength(1);

    const [entry] = await outbox.pending();
    expect(entry.operationType).toBe('createTreatmentCourse');
    // The payload's field names are the wire contract with
    // CreateTreatmentCourseCommand — see TreatmentCourseInput's doc comment.
    expect(entry.payload).toMatchObject({
      animalId: 'pig-1',
      routeId: 'route-im',
      reason: 'scheduled',
      productId: 'item-1',
      doseKindId: 'dose-per-head',
      doseFactorAmount: 1,
      doseFactorUnit: 'dosis',
      isPlausibilityConfirmed: false,
    });

    // Screen fell back to the animal picker — reset() actually landed.
    expect(screen.queryByTestId('vaccinate-confirm')).toBeNull();
  });

  it('leaves no dirty state when cancelled after picking an animal', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    expect(await screen.findByTestId('vaccinate-product-item-1')).toBeTruthy();

    fireEvent.press(screen.getByTestId('vaccinate-cancel'));

    // Back at the animal picker — nothing pre-selected.
    expect(await screen.findByTestId('vaccinate-animal-pig-1')).toBeTruthy();
    expect(screen.queryByTestId('vaccinate-product-item-1')).toBeNull();
    expect(await outbox.pending()).toHaveLength(0);
  });

  it('shows an empty-catalog message instead of a broken picker when there are no products', async () => {
    await render(
      <VaccinateScreen
        service={service}
        database={database}
        animals={animals}
        products={[]}
        onCancel={() => undefined}
      />,
    );

    fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
    expect(await screen.findByTestId('vaccinate-product-empty')).toBeTruthy();
  });
  /**
   * feature-0006 commit 2. Vaccination is short by design (three taps), but the
   * confirm card is still the step with no inner scroller: a long animal label,
   * the missing-catalog warning and an error notice all stack in the same fixed
   * box, and "Confirmar" is what goes under the fold on a short screen. The
   * picker steps keep the bounded scroller they already had.
   */
  describe('reachability of the confirm step (feature-0006)', () => {
    it('scrolls the confirm step and keeps "Confirmar" pressable', async () => {
      await render(
        <VaccinateScreen
          service={service}
          database={database}
          animals={animals}
          products={products}
          onCancel={() => undefined}
        />,
      );

      fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
      fireEvent.press(await screen.findByTestId('vaccinate-product-item-1'));
      const confirmButton = await screen.findByTestId('vaccinate-confirm');

      // One vertical gesture, owned by the Screen (D1).
      expect(countScrollers(screen.toJSON())).toBe(1);

      const content = flatten(screen.getByTestId('vaccinate-screen').props.contentContainerStyle);
      expect(content.flexGrow).toBe(1);
      // `flex: 1` clamps the content box back to the viewport — the defect itself.
      expect(content.flex).toBeUndefined();
      // And the `styles.body` wrapper must not re-clamp it from the inside.
      expect(clampsInsideScreen('vaccinate-screen', screen.toJSON())).not.toContain(1);

      // The footer action travels with the content instead of being pinned below it.
      expect(screen.getByTestId('vaccinate-cancel')).toBeTruthy();

      await act(async () => {
        fireEvent.press(confirmButton);
        await Promise.resolve();
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(await outbox.pending()).toHaveLength(1);
    });

    it('leaves the picker steps with their bounded inner scroller and no second one', async () => {
      await render(
        <VaccinateScreen
          service={service}
          database={database}
          animals={animals}
          products={products}
          onCancel={() => undefined}
        />,
      );

      expect(await screen.findByTestId('vaccinate-animal-list')).toBeTruthy();
      expect(countScrollers(screen.toJSON())).toBe(1);
      // A plain View, not a scroller: nothing competes with the list for the drag.
      expect(screen.getByTestId('vaccinate-screen').props.contentContainerStyle).toBeUndefined();

      fireEvent.press(screen.getByTestId('vaccinate-animal-pig-1'));
      expect(await screen.findByTestId('vaccinate-product-list')).toBeTruthy();
      expect(countScrollers(screen.toJSON())).toBe(1);
      expect(screen.getByTestId('vaccinate-screen').props.contentContainerStyle).toBeUndefined();
    });

    it('scrolls a step whose picker collapsed into an empty state', async () => {
      await render(
        <VaccinateScreen
          service={service}
          database={database}
          animals={animals}
          products={[]}
          onCancel={() => undefined}
        />,
      );

      fireEvent.press(await screen.findByTestId('vaccinate-animal-pig-1'));
      expect(await screen.findByTestId('vaccinate-product-empty')).toBeTruthy();

      // No inner list here, so the Screen takes the gesture — otherwise the
      // empty-state card plus "Cancelar" have nowhere to go on a short phone.
      expect(countScrollers(screen.toJSON())).toBe(1);
      expect(flatten(screen.getByTestId('vaccinate-screen').props.contentContainerStyle).flexGrow).toBe(1);
      expect(screen.getByTestId('vaccinate-cancel')).toBeTruthy();
    });
  });
});
