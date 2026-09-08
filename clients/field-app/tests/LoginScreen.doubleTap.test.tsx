import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { LoginScreen } from '../src/screens/LoginScreen';
import type { AuthService } from '../src/services/authService';

/**
 * feature-0006 commit 4 (T4.2), spec D2.
 *
 * Nothing here reaches the outbox, but the gesture is the same class: two taps on
 * "Iniciar sesión" used to fire two network attempts for one intention, and on the
 * farm's signal the second one answers long after the first has already moved the
 * employee into the app. The screen uses the same `useSingleFlight` latch as every
 * recording screen so the rule does not have exceptions to remember.
 */
describe('LoginScreen — double tap (feature-0006 T4.2)', () => {
  const gated = () => {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  };

  it('attempts one login when the button is tapped twice while the request is open', async () => {
    const gate = gated();
    const login = jest.fn(async () => {
      await gate.promise;
      return {} as never;
    });
    const auth = { login } as unknown as AuthService;
    const onAuthenticated = jest.fn();

    await render(
      <LoginScreen auth={auth} hasCachedSession={false} onAuthenticated={onAuthenticated} />,
    );

    const button = await screen.findByTestId('login');
    await act(async () => {
      fireEvent.press(button);
      fireEvent.press(button);
    });
    await act(async () => {
      gate.release();
    });

    expect(login).toHaveBeenCalledTimes(1);
    // T4.3: one visible result per operation — the app is entered once, not twice.
    expect(onAuthenticated).toHaveBeenCalledTimes(1);
  });

  it('attempts one PIN unlock when the button is tapped twice while the check runs', async () => {
    const gate = gated();
    const unlockWithPin = jest.fn(async () => {
      await gate.promise;
      return {} as never;
    });
    const auth = { unlockWithPin } as unknown as AuthService;

    await render(
      <LoginScreen auth={auth} hasCachedSession onAuthenticated={() => undefined} />,
    );

    const button = await screen.findByTestId('unlock-with-pin');
    await act(async () => {
      fireEvent.press(button);
      fireEvent.press(button);
    });
    await act(async () => {
      gate.release();
    });

    expect(unlockWithPin).toHaveBeenCalledTimes(1);
  });
});
