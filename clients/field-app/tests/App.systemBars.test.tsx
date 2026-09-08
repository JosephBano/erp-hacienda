import React from 'react';
import * as SecureStore from 'expo-secure-store';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { theme } from '../src/ui/theme';

/**
 * feature-0006 commit 2 — the system navigation bar.
 *
 * The employees' report was specific: on the home screen, once the menu grew, the last
 * entry ended up under something else. The cause is structural. `SafeAreaView` from
 * react-native applies insets on iOS only — on Android it is a plain View — and the app
 * compensated for Android's navigation bar in the footer, which the home screen is the
 * one screen that does not render. So the hub's last button sat behind the back / home /
 * recents buttons with nothing to push it up.
 *
 * These tests pin the correction where it belongs: on the root container that every
 * branch of App shares. Putting the inset back on the footer, or dropping it, fails here.
 * Like every test in this suite it renders through the mock host and measures no pixels;
 * the on-device check is `test-e2e.md`.
 */

// The RN Jest preset resolves the iOS flavour of Platform, whose `select` is hard-wired
// to the iOS branch. The defect only exists on Android, so the platform is the fixture.
jest.mock('react-native/Libraries/Utilities/Platform', () => ({
  __esModule: true,
  default: {
    OS: 'android',
    Version: 34,
    isTesting: true,
    isTV: false,
    constants: { reactNativeVersion: { major: 0, minor: 85, patch: 3 } },
    select: (spec: Record<string, unknown>) =>
      'android' in spec ? spec.android : 'native' in spec ? spec.native : spec.default,
  },
}));

// The real database is SQLite through a native adapter, and the real sync engine talks to
// the network. Neither has anything to do with where the app reserves the system bar.
jest.mock('../src/database', () => ({ createDatabase: () => ({}) }));

jest.mock('../src/services/herdQueries', () => ({
  loadHerd: async () => [],
  loadGroups: async () => [],
  loadTreatmentProducts: async () => [],
  loadMedications: async () => [],
  loadMortalityCauses: async () => [],
  loadFeedItems: async () => [],
  loadPregnantDams: async () => [],
  loadBreeds: async () => [],
  loadCategories: async () => [],
}));

jest.mock('../src/services/outbox', () => ({
  Outbox: class {
    async stats() {
      return { pending: 0, synced: 0, rejected: 0, cancelled: 0 };
    }
    async today() {
      return [];
    }
    async rejected() {
      return [];
    }
  },
}));

jest.mock('../src/services/moduleVisibility', () => ({
  ModuleVisibility: class {
    async canShow() {
      return true;
    }
  },
}));

jest.mock('../src/services/syncEngine', () => ({
  SyncEngine: class {
    start() {}
    stop() {}
    async syncNow() {
      return { pushed: 0, pulled: 0 };
    }
  },
}));

// eslint-disable-next-line @typescript-eslint/no-var-requires
const App = require('../src/App').default;

const PIN = '1234';
const SALT = 'salt-for-the-test';

/** Same digest AuthService computes, so the cached session unlocks with no network. */
const pinHash = () =>
  require('crypto').createHash('sha256').update(`${SALT}:${PIN}`).digest('hex') as string;

const flatten = (style: unknown): Record<string, unknown> =>
  Array.isArray(style)
    ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
    : ((style ?? {}) as Record<string, unknown>);

/** Signs in through the cached-session path: the morning case, and the only offline one. */
const openHome = async () => {
  await render(<App />);

  const input = await screen.findByTestId('pin-input');
  fireEvent.changeText(input, PIN);
  await waitFor(() => expect(screen.getByTestId('pin-input').props.value).toBe(PIN));

  fireEvent.press(screen.getByTestId('unlock-with-pin'));

  await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
};

describe('App — system navigation bar', () => {
  beforeEach(async () => {
    (global as any).__resetNativeMocks();

    await SecureStore.setItemAsync(
      'hato_session',
      JSON.stringify({
        userId: 'user-1',
        fullName: 'Empleado',
        email: 'empleado@hato.test',
        token: 't',
        refreshToken: 'r',
        expiresAt: '2099-01-01T00:00:00.000Z',
        roles: [],
        permissions: [],
        pinHash: pinHash(),
        pinSalt: SALT,
      }),
    );

    global.fetch = jest.fn(() => {
      throw new Error('Abrir la app con PIN no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('reserves the Android navigation bar at the root, so the home screen clears it too', async () => {
    await openHome();

    const root = flatten(screen.getByTestId('app-root').props.style);

    // The home screen renders no footer — that is precisely why the inset cannot live
    // there. 48 units is the height of the three-button navigation bar.
    expect(screen.queryByTestId('app-footer')).toBeNull();
    expect(root.paddingBottom).toBe(48);
  });

  it('keeps the hub last entry pressable instead of leaving it under the system buttons', async () => {
    await openHome();

    // "Sincronización" is the button the employees found unreachable: last child of the
    // last card of the screen that has no footer to push it up.
    fireEvent.press(screen.getByTestId('subject-sync'));

    await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());
  });

  it('does not pay the inset twice on the screens that do have a footer', async () => {
    await openHome();

    fireEvent.press(screen.getByTestId('subject-today'));

    const footer = flatten((await screen.findByTestId('app-footer')).props.style);

    // The root already cleared the system bar; the footer only keeps its own breathing
    // room. Adding the bar height here again would push "Inicio" 48 units into the air.
    expect(footer.paddingBottom).toBe(theme.space.md);
    expect(flatten(screen.getByTestId('app-root').props.style).paddingBottom).toBe(48);
  });
});
