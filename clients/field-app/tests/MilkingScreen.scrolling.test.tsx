import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * feature-0006 commit 2 — the milking screen has two modes and each one gets exactly one
 * vertical gesture (D1).
 *
 * The picker owns a bounded `cow-list` scroller with the daily total pinned below it,
 * which is the right shape for the 5 AM screen; making the whole screen scroll there
 * would nest two scrollers and the inner one would trap the drag. The selected-animal
 * form owns no scroller at all and grows with the withdrawal notice, the plausibility
 * notice and its two extra buttons — so there the screen itself scrolls.
 *
 * These are structural assertions, not layout measurements: RTL renders through the mock
 * host and cannot tell whether a pixel is on screen. What they pin is the shape that
 * *causes* unreachable content, so a later edit cannot bring it back quietly. The
 * on-device check is `test-e2e.md` and it is the one that closes the spec.
 */
describe('MilkingScreen scrolling', () => {
  let database: Database;
  let service: MilkingService;

  const speciesId = 'species-bovino';
  const candidates = [
    { animalId: 'cow-1', label: 'La Pinta', isWithheld: false, speciesIsMilkable: true, speciesId },
  ];

  /** Counts the host scrollers in the rendered tree — the D1 "one vertical gesture" check. */
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

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-milking-scrolling-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    service = new MilkingService(database);

    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = 'Bovino';
        row.isMilkable = true;
        row.isDeleted = false;
      });
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-1';
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de ordeño no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('keeps the picker on its own bounded list and does not open a second scroller', async () => {
    await render(
      <MilkingScreen service={service} database={database} candidates={candidates} recordedBy="tester@hato" />,
    );

    expect(await screen.findByTestId('cow-list')).toBeTruthy();
    // One, not two: the `cow-list` scroller is the picker's, and the Screen must stay
    // fixed so the daily total remains pinned at the foot and no drag is trapped.
    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('scrolls the whole screen once a cow is selected, with nothing nested inside', async () => {
    await render(
      <MilkingScreen service={service} database={database} candidates={candidates} recordedBy="tester@hato" />,
    );

    await act(async () => {
      fireEvent.press(await screen.findByTestId('cow-cow-1'));
    });

    // The picker's list is gone and the Screen's own scroller replaces it: still one.
    expect(screen.queryByTestId('cow-list')).toBeNull();
    expect(countScrollers(screen.toJSON())).toBe(1);

    const scroller = screen.getByTestId('milking-screen');
    const content = flatten(scroller.props.contentContainerStyle);
    // `flex: 1` would clamp the form back to the window height — the very defect the
    // scroll is here to fix. `flexGrow: 1` still fills a short screen.
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
    // Without 'handled', the first tap on "Registrar" with the numeric pad open is
    // spent dismissing the keyboard and never reaches the button.
    expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');
  });

  it('keeps the form tail reachable and pressable with the whole card rendered', async () => {
    await render(
      <MilkingScreen service={service} database={database} candidates={candidates} recordedBy="tester@hato" />,
    );

    await act(async () => {
      fireEvent.press(await screen.findByTestId('cow-cow-1'));
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('liters-input'), '12.5');
    });

    // "Cancelar" is the last control of the tallest form state; it must still be in the
    // tree and still respond, not be clipped away below the fold.
    await act(async () => {
      fireEvent.press(screen.getByTestId('cancel-milking'));
    });

    // Cancelling returns to the picker, which means back to its bounded list — the
    // mode switch works in both directions.
    expect(await screen.findByTestId('cow-list')).toBeTruthy();
    expect(countScrollers(screen.toJSON())).toBe(1);
  });
});
