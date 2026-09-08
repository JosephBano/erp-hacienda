import React from 'react';
import { act, fireEvent, render } from '@testing-library/react-native';

import { BigButton, Screen } from '../src/ui/components';
import { useSingleFlight } from '../src/ui/useSingleFlight';

/**
 * feature-0006 commit 4, D2 — "confirmar repetidamente mientras se guarda
 * produce una sola operación local y un único resultado visible".
 *
 * These tests are the guarantee that holds up the claim; without them the
 * commit does not go in (tasks T4.2). They exercise the latch against the two
 * shapes the app actually has: a confirm that awaits a local check before
 * writing (the plausibility paths), and one that writes straight away.
 *
 * Every press and every release is wrapped in an explicit `act`, and the
 * assertions run after it rather than inside `waitFor`. Both matter: a press
 * here leaves an async continuation that ends in `setBusy`, and letting that
 * land inside a *second*, concurrent act scope is what produces React 19's
 * "overlapping act() calls" and leaves the next test rendering into a torn-down
 * tree.
 */
describe('useSingleFlight', () => {
  /** A confirm button of the shape every recording screen uses. */
  function ConfirmButton({ record }: { record: () => Promise<void> }) {
    const { busy, runOnce } = useSingleFlight();
    return (
      <Screen testID="form" scrollable>
        <BigButton
          testID="confirm"
          label="Registrar"
          busy={busy}
          // Every real screen catches inside its own operation and turns the
          // failure into a `Notice`; the hook deliberately does not swallow.
          // This stands in for that so a deliberately-throwing save under test
          // does not surface as an unhandled rejection.
          onPress={() => {
            runOnce(record).catch(() => undefined);
          }}
        />
      </Screen>
    );
  }

  const isDisabled = (view: ReturnType<typeof render> extends Promise<infer T> ? T : never) =>
    view.getByTestId('confirm').props.accessibilityState.disabled;

  /** A save the test decides when to finish, standing in for a slow local write. */
  const gated = () => {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  };

  it('drops the taps that land while the first save is still in flight', async () => {
    const written: string[] = [];
    const gate = gated();

    const view = await render(
      <ConfirmButton
        record={async () => {
          await gate.promise;
          written.push('entry');
        }}
      />,
    );

    const confirm = view.getByTestId('confirm');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });

    await act(async () => {
      gate.release();
    });

    // Three taps, one entry. History is immutable (regla dura 1): a duplicate
    // here is a correction event an employee has to file, not a row to delete.
    expect(written).toEqual(['entry']);
  });

  it('drops the second tap even when the save awaits before writing anything', async () => {
    // The plausibility shape: MilkingScreen.record and TreatScreen.confirm both
    // await a local range check before enqueuing. That await used to be a real
    // window with the button still enabled, wide enough for a gloved double tap.
    const written: string[] = [];

    const view = await render(
      <ConfirmButton
        record={async () => {
          await Promise.resolve();
          await Promise.resolve();
          written.push('entry');
        }}
      />,
    );

    const confirm = view.getByTestId('confirm');
    await act(async () => {
      fireEvent.press(confirm);
      fireEvent.press(confirm);
    });

    expect(written).toEqual(['entry']);
  });

  it('lets the employee record again once the first save has finished', async () => {
    const written: string[] = [];
    const view = await render(<ConfirmButton record={async () => { written.push('entry'); }} />);

    const confirm = view.getByTestId('confirm');
    await act(async () => {
      fireEvent.press(confirm);
    });
    await act(async () => {
      fireEvent.press(confirm);
    });

    expect(written).toEqual(['entry', 'entry']);
  });

  it('releases the latch when the save fails, so a retry is possible', async () => {
    let attempts = 0;
    const view = await render(
      <ConfirmButton
        record={async () => {
          attempts += 1;
          if (attempts === 1) throw new Error('sin espacio en disco');
        }}
      />,
    );

    const confirm = view.getByTestId('confirm');
    // The screen owns its error handling; the latch must not stay shut because a
    // save failed, or the employee is locked out of retrying in the paddock.
    await act(async () => {
      fireEvent.press(confirm);
    });
    await act(async () => {
      fireEvent.press(confirm);
    });

    expect(attempts).toBe(2);
  });

  it('shows the save in flight and stops showing it when it lands', async () => {
    const gate = gated();
    const view = await render(<ConfirmButton record={() => gate.promise} />);

    expect(isDisabled(view)).toBe(false);

    await act(async () => {
      fireEvent.press(view.getByTestId('confirm'));
    });
    // D2's other half: the phone tells the employee a local save is happening.
    expect(isDisabled(view)).toBe(true);

    await act(async () => {
      gate.release();
    });
    expect(isDisabled(view)).toBe(false);
  });
});
