import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { EventService } from '../src/services/eventService';
import { TreatScreen } from '../src/screens/TreatScreen';

/**
 * docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md, test 5: the curative path is deeper than
 * vaccination but still bounded — exactly four taps: animal, product, the
 * combined form (one pass, same accounting `MilkingScreen` uses for "type
 * the litres"), confirm.
 */
/**
 * feature-0006 commit 2 — structural helpers, copied from `Screen.test.tsx` on
 * purpose. RNTL renders through the mock host and measures nothing, so none of
 * these prove a pixel is on screen; what they pin is the *structure* that makes
 * content unreachable — a second vertical scroller stealing the drag, and a
 * clamped content box capping the form at one window. The on-device check is
 * `test-e2e.md` and it is the one that closes the spec (criterion 6).
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

describe('TreatScreen', () => {
  let database: Database;
  let outbox: Outbox;
  let service: EventService;

  const animals = [{ animalId: 'pig-1', label: 'Cerdo 01', speciesId: 'species-1', categoryId: null }];
  const products = [{ itemId: 'item-1', name: 'Antibiótico X', unit: 'ml' }];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-treat-${Math.random()}`,
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
      await database.get('treatment_reasons').create((row: any) => {
        row._raw.id = 'reason-curative';
        row.key = 'curative';
        row.labelEs = 'Curativo';
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
    });

    // Registering a treatment must never depend on reaching the server (Art. 9).
    global.fetch = jest.fn(() => {
      throw new Error('TreatScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('requires notes when no numeric dose is entered', async () => {
    await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    await fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    await fireEvent.press(await screen.findByTestId('treat-continue'));

    expect(await screen.findByText(/las notas son obligatorias/)).toBeTruthy();
    expect(screen.queryByTestId('treat-confirm')).toBeNull();
  });

  it('leaves no dirty state when cancelled mid-form', async () => {
    await render(
      <TreatScreen
        service={service}
        database={database}
        animals={animals}
        products={products}
        onCancel={() => undefined}
      />,
    );

    await fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    await fireEvent.changeText(await screen.findByTestId('treat-dose-input'), '5');

    await fireEvent.press(screen.getByTestId('treat-cancel'));

    // Back at the animal picker, form state wiped.
    expect(await screen.findByTestId('treat-animal-pig-1')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('treat-animal-pig-1'));
    await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
    const doseInput = await screen.findByTestId('treat-dose-input');
    expect(doseInput.props.value).toBe('');
    expect(await outbox.pending()).toHaveLength(0);
  });
  /**
   * feature-0006 commit 2. The form step is the one that overflows: it draws
   * every administration route and every reason as a 64-unit `BigButton`, so its
   * height is a function of the catalog, not of the design. With the spec's
   * stress catalog (sec. 5: 8 vías, 6 motivos) that is 14 × 64 = 896 units of
   * buttons before the dose field, the notes field and "Continuar" — well past
   * any phone. These tests pin the fix: the form and confirm steps get the
   * Screen's own scroller, the picker steps keep their bounded inner one, and
   * neither render opens two.
   */
  describe('reachability of the long steps (feature-0006)', () => {
    /** The spec's stress catalog: 8 routes and 6 reasons (spec sec. 5). */
    const seedStressCatalog = async () => {
      const routes = ['im', 'sc', 'iv', 'oral', 'topica', 'intramamaria', 'intrauterina', 'ocular'];
      const reasons = ['curative', 'preventive', 'scheduled', 'metafilaxis', 'control', 'other'];

      await database.write(async () => {
        for (const [index, key] of routes.entries()) {
          if (key === 'im') continue; // already seeded by the outer beforeEach
          await database.get('administration_routes').create((row: any) => {
            row._raw.id = `route-${key}`;
            row.key = key;
            row.labelEs = `Vía ${index} ${key}`;
            row.isActive = true;
            row.isDeleted = false;
          });
        }
        for (const [index, key] of reasons.entries()) {
          if (key === 'curative') continue; // already seeded by the outer beforeEach
          await database.get('treatment_reasons').create((row: any) => {
            row._raw.id = `reason-${key}`;
            row.key = key;
            row.labelEs = `Motivo ${index} ${key}`;
            row.isActive = true;
            row.isDeleted = false;
          });
        }
      });
    };

    const openForm = async () => {
      await fireEvent.press(await screen.findByTestId('treat-animal-pig-1'));
      await fireEvent.press(await screen.findByTestId('treat-product-item-1'));
      return screen.findByTestId('treat-continue');
    };

    it('keeps "Continuar" reachable with the stress catalog of 8 vías and 6 motivos', async () => {
      await seedStressCatalog();

      await render(
        <TreatScreen
          service={service}
          database={database}
          animals={animals}
          products={products}
          onCancel={() => undefined}
        />,
      );

      const continueButton = await openForm();

      // Every catalog row really is drawn — this is the height that overflows.
      expect(screen.getByTestId('treat-route-picker').props.children).toHaveLength(8);
      expect(screen.getByTestId('treat-reason-picker').props.children).toHaveLength(6);

      // One vertical gesture, and it belongs to the Screen: the form step owns no
      // inner list, so nothing competes for the drag (D1).
      expect(countScrollers(screen.toJSON())).toBe(1);

      const content = flatten(screen.getByTestId('treat-screen').props.contentContainerStyle);
      expect(content.flexGrow).toBe(1);
      // `flex: 1` would clamp the content box back to the viewport, which is the
      // defect itself: the tail of the form below the fold with nothing to scroll.
      expect(content.flex).toBeUndefined();
      // Same clamp one level down: the `styles.body` wrapper must not re-bound the
      // form to one window from the inside.
      expect(clampsInsideScreen('treat-screen', screen.toJSON())).not.toContain(1);

      // The last control is in the tree and answers a press: the validation message
      // is proof the tap reached `continueToConfirm` rather than being swallowed.
      await fireEvent.press(continueButton);
      expect(await screen.findByText(/las notas son obligatorias/)).toBeTruthy();

      // The footer action rides along in the same scroller instead of being pinned
      // off-screen below it.
      expect(screen.getByTestId('treat-cancel')).toBeTruthy();
    });

    it('leaves the picker steps with their bounded inner scroller and no second one', async () => {
      await render(
        <TreatScreen
          service={service}
          database={database}
          animals={animals}
          products={products}
          onCancel={() => undefined}
        />,
      );

      // Animal step: the list scroller is the only one, and the Screen is a plain
      // View (no contentContainerStyle) so the drag is not contested.
      expect(await screen.findByTestId('treat-animal-list')).toBeTruthy();
      expect(countScrollers(screen.toJSON())).toBe(1);
      expect(screen.getByTestId('treat-screen').props.contentContainerStyle).toBeUndefined();

      // Product step: same shape.
      await fireEvent.press(screen.getByTestId('treat-animal-pig-1'));
      expect(await screen.findByTestId('treat-product-list')).toBeTruthy();
      expect(countScrollers(screen.toJSON())).toBe(1);
      expect(screen.getByTestId('treat-screen').props.contentContainerStyle).toBeUndefined();
    });

    it('scrolls the confirm step and still records on a single "Confirmar"', async () => {
      await render(
        <TreatScreen
          service={service}
          database={database}
          animals={animals}
          products={products}
          onCancel={() => undefined}
        />,
      );

      const continueButton = await openForm();
      await fireEvent.changeText(await screen.findByTestId('treat-dose-input'), '5');
      await fireEvent.press(continueButton);

      const confirmButton = await screen.findByTestId('treat-confirm');
      expect(countScrollers(screen.toJSON())).toBe(1);
      expect(flatten(screen.getByTestId('treat-screen').props.contentContainerStyle).flexGrow).toBe(1);
      expect(clampsInsideScreen('treat-screen', screen.toJSON())).not.toContain(1);

      // Same act()+microtask dance as VaccinateScreen.test.tsx: the press starts an
      // async chain (plausibility read, outbox write, reset) that must flush.
      await act(async () => {
        fireEvent.press(confirmButton);
        await Promise.resolve();
        await Promise.resolve();
        await Promise.resolve();
      });

      expect(await outbox.pending()).toHaveLength(1);
    });
  });
});
