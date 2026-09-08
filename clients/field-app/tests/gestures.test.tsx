import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * feature-0006 commit 4, T4.1 — "deslizar sobre una lista o un botón no
 * registra nada".
 *
 * What this can and cannot prove is worth stating plainly. React Native
 * Testing Library dispatches synthetic events; it does not run the responder
 * system, so it cannot reproduce a real finger that presses a row and then
 * drags. The part of "a drag registers nothing" that belongs to React Native —
 * `Pressable` cancelling its press once the touch travels past the slop
 * threshold — is framework behaviour, and a test here would be testing React
 * Native, not this app.
 *
 * What *is* ours, and what these pin, is that scrolling a picker is inert: no
 * animal gets selected, nothing reaches the outbox, and the scroller is
 * configured to put the keyboard down on a drag instead of letting the drag do
 * something else. The real gesture check is E2E-2 in `test-e2e.md`, on a phone.
 */
describe('gestures record nothing', () => {
  let database: Database;
  let outbox: Outbox;

  const candidates = Array.from({ length: 12 }, (_, index) => ({
    animalId: `a-${index}`,
    label: `Vaca ${index}`,
    isWithheld: false,
    speciesIsMilkable: true,
  }));

  beforeEach(() => {
    database = new Database({
      adapter: new LokiJSAdapter({ schema, migrations, useWebWorker: false, useIncrementalIndexedDB: false }),
      modelClasses,
    });
    outbox = new Outbox(database);
  });

  it('scrolling the cow picker selects nothing and enqueues nothing', async () => {
    const view = await render(
      <MilkingScreen
        service={new MilkingService(database)}
        database={database}
        candidates={candidates}
        recordedBy="empleado@hato.test"
      />,
    );

    const list = view.getByTestId('cow-list');

    await act(async () => {
      fireEvent.scroll(list, {
        nativeEvent: {
          contentOffset: { y: 420, x: 0 },
          contentSize: { height: 900, width: 360 },
          layoutMeasurement: { height: 640, width: 360 },
        },
      });
    });

    // Still the picker: no cow was chosen by the act of scrolling past her.
    expect(view.getByTestId('cow-list')).toBeTruthy();
    expect(view.queryByTestId('liters-input')).toBeNull();
    expect((await outbox.stats()).pending).toBe(0);
  });

  it('puts the keyboard down on a drag rather than letting the drag do something else', async () => {
    await render(
      <MilkingScreen
        service={new MilkingService(database)}
        database={database}
        candidates={candidates}
        recordedBy="empleado@hato.test"
      />,
    );

    await act(async () => {
      fireEvent.press(screen.getByTestId('cow-a-0'));
    });
    const form = screen.getByTestId('milking-screen');

    // D2, the drag half: scrolling away from the litres field dismisses the
    // keyboard and registers nothing. 'handled' is the other half — the tap that
    // follows reaches the button instead of being spent closing the keyboard.
    expect(form.props.keyboardDismissMode).toBe('on-drag');
    expect(form.props.keyboardShouldPersistTaps).toBe('handled');
    expect((await outbox.stats()).pending).toBe(0);
  });
});
