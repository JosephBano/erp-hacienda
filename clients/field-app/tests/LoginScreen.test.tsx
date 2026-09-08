import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { LoginScreen } from '../src/screens/LoginScreen';
import type { AuthService } from '../src/services/authService';

/**
 * feature-0006 commit 2 — the login screen had no component test at all, and it
 * is the one screen every employee meets before anything else works. What is
 * pinned here is reachability, not the auth contract (that lives in
 * `tests/auth.test.ts`): with the keyboard up on the short tablet the employees
 * use, "Iniciar sesión" sat under it with nothing to drag, and React Native's
 * default `keyboardShouldPersistTaps="never"` spent the first tap dismissing
 * the keyboard instead of delivering it.
 *
 * RTL renders through the mock host and measures no layout; `test-e2e.md` on a
 * real phone is what closes the spec (criterion 6).
 */
describe('LoginScreen', () => {
  const flatten = (style: unknown): Record<string, unknown> =>
    Array.isArray(style)
      ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
      : ((style ?? {}) as Record<string, unknown>);

  const countScrollers = (node: unknown): number => {
    if (!node || typeof node !== 'object') return 0;
    const element = node as { type?: string; children?: unknown[] };
    const self = element.type === 'RCTScrollView' || element.type === 'ScrollView' ? 1 : 0;
    return (element.children ?? []).reduce<number>((acc, child) => acc + countScrollers(child), self);
  };

  /**
   * Only the two entry points this screen calls. The real `AuthService` returns a
   * `UserSession`; nothing here reads it, so the stub answers with nothing and the
   * cast is confined to this one line.
   */
  const authStub = (overrides: Record<string, () => Promise<void>> = {}) =>
    ({
      login: async () => undefined,
      unlockWithPin: async () => undefined,
      ...overrides,
    }) as unknown as AuthService;

  it('scrolls, so the sign-in button is reachable with the keyboard open', async () => {
    await render(<LoginScreen auth={authStub()} hasCachedSession onAuthenticated={() => undefined} />);

    const scroller = screen.getByTestId('login-screen');
    expect(countScrollers(screen.toJSON())).toBe(1);
    expect(flatten(scroller.props.contentContainerStyle).flexGrow).toBe(1);
    expect(flatten(scroller.props.contentContainerStyle).flex).toBeUndefined();
    // The reason the employee's first tap on "Iniciar sesión" does nothing today.
    expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');
  });

  it('keeps both ways in reachable when the phone has a cached session', async () => {
    const attempted: string[] = [];
    await render(
      <LoginScreen
        auth={authStub({
          login: async () => {
            attempted.push('login');
          },
          unlockWithPin: async () => {
            attempted.push('pin');
          },
        })}
        hasCachedSession
        onAuthenticated={() => undefined}
      />,
    );

    // Two cards stacked plus a possible error notice is the tallest this screen
    // gets, and the password path is the one at the bottom.
    fireEvent.press(screen.getByTestId('login'));
    expect(attempted).toEqual(['login']);
  });
});
