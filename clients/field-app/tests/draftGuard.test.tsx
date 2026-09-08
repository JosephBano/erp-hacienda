import React, { useState } from 'react';
import { act, fireEvent, render } from '@testing-library/react-native';

import { BigButton, Screen, TextField } from '../src/ui/components';
import { DraftGuardProvider, useDraftFlag, useDraftGate } from '../src/ui/draftGuard';

/**
 * feature-0006 commit 5, D3 — "abandonarlo con cambios advierte antes de
 * descartarlos", and criterion 4's "salida explícita y comprensible".
 *
 * The shell owns navigation and cannot see inside a screen's form state, so the
 * screen reports "something is unsaved here" and the shell asks before it acts
 * on an ambiguous exit ("Inicio", hardware back). An explicit "Cancelar" inside
 * a screen is already the employee saying it, and is deliberately not gated.
 */
describe('draft guard', () => {
  function Form() {
    const [dose, setDose] = useState('');
    useDraftFlag(dose.trim().length > 0);
    return <TextField label="Dosis" testID="dose" value={dose} onChangeText={setDose} />;
  }

  function Shell({ onLeave, showForm = true }: { onLeave: () => void; showForm?: boolean }) {
    const gate = useDraftGate();
    return (
      <Screen testID="shell" scrollable>
        <DraftGuardProvider onDirtyChange={gate.markDirty}>
          {showForm ? <Form /> : null}
        </DraftGuardProvider>
        <BigButton testID="leave" label="Inicio" onPress={() => gate.request(onLeave)} />
        {gate.isAsking ? (
          <>
            <BigButton testID="keep" label="Seguir aquí" onPress={gate.keep} />
            <BigButton testID="discard" label="Salir y descartar" onPress={gate.discard} />
          </>
        ) : null}
      </Screen>
    );
  }

  const type = async (view: { getByTestId: (id: string) => unknown }, text: string) => {
    await act(async () => {
      fireEvent.changeText(view.getByTestId('dose') as never, text);
    });
  };

  it('leaves immediately when nothing has been typed', async () => {
    const left: string[] = [];
    const view = await render(<Shell onLeave={() => left.push('home')} />);

    fireEvent.press(view.getByTestId('leave'));

    // No question: warning on every exit trains the employee to tap through it.
    expect(left).toEqual(['home']);
    expect(view.queryByTestId('discard')).toBeNull();
  });

  it('asks instead of discarding what the employee typed', async () => {
    const left: string[] = [];
    const view = await render(<Shell onLeave={() => left.push('home')} />);

    await type(view, '12');
    await act(async () => {
      fireEvent.press(view.getByTestId('leave'));
    });

    expect(left).toEqual([]);
    expect(view.getByTestId('discard')).toBeTruthy();
  });

  it('keeps the employee on the screen when they say so', async () => {
    const left: string[] = [];
    const view = await render(<Shell onLeave={() => left.push('home')} />);

    await type(view, '12');
    await act(async () => {
      fireEvent.press(view.getByTestId('leave'));
    });
    await act(async () => {
      fireEvent.press(view.getByTestId('keep'));
    });

    expect(left).toEqual([]);
    // The dose is still there: saying "stay" must not cost the entry either.
    expect((view.getByTestId('dose') as unknown as { props: { value: string } }).props.value).toBe('12');
  });

  it('leaves once, and only once, when they choose to discard', async () => {
    const left: string[] = [];
    const view = await render(<Shell onLeave={() => left.push('home')} />);

    await type(view, '12');
    await act(async () => {
      fireEvent.press(view.getByTestId('leave'));
    });
    await act(async () => {
      fireEvent.press(view.getByTestId('discard'));
    });

    expect(left).toEqual(['home']);
  });

  it('forgets the flag when the screen it belonged to goes away', async () => {
    const left: string[] = [];
    const view = await render(<Shell onLeave={() => left.push('home')} />);

    await type(view, '12');
    // The screen unmounts — a screen that is gone cannot lose anything, and a
    // stale flag would make the *next* screen inherit a warning about this one.
    await act(async () => {
      view.rerender(<Shell onLeave={() => left.push('home')} showForm={false} />);
    });

    await act(async () => {
      fireEvent.press(view.getByTestId('leave'));
    });
    expect(left).toEqual(['home']);
  });
});
