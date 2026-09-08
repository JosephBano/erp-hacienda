import React from 'react';
import * as SecureStore from 'expo-secure-store';
import { BackHandler } from 'react-native';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

/**
 * feature-0006 commit 5 — Android's hardware back button.
 *
 * There was no handler for it at all. Pressing back anywhere in the app handed
 * the press to the OS, which closed the app: the employee lost the screen they
 * were on, and any draft with it, from one thumb press they did not mean as
 * "quit". Tasks T5.3 asks for the hardware button and the on-screen one to give
 * coherent results and to leave no route unreachable, which is why both now run
 * the same `goHome`.
 *
 * The hub is the one place back still belongs to the OS — that is where leaving
 * the app is the thing the employee actually means.
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
  loadActiveHerd: async () => [],
  loadMilkingCandidates: async () => [],
  loadGroups: async () => [],
  loadTreatmentProducts: async () => [],
  loadMedications: async () => [],
  loadMortalityCauses: async () => [],
  loadFeedItems: async () => [],
  loadPregnantDams: async () => [
    {
      animalId: 'dam-1',
      label: 'La Pinta',
      pregnancyId: 'preg-1',
      sireLabel: 'Padre: Inseminación artificial',
      expectedBirthDate: '2026-09-01',
    },
  ],
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
    subscribe() {
      return () => {};
    }
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


/**
 * Captures the handler App registers, so a test can press back the way the OS
 * does. The return value matters as much as the side effect: `false` is how a
 * handler says "not mine — close the app".
 */
const backPresses: (() => boolean)[] = [];
const pressBack = () => {
  const handler = backPresses[backPresses.length - 1];
  if (!handler) throw new Error('App registered no hardware back handler.');
  return handler();
};

/** Signs in through the cached-session path: the morning case, and the only offline one. */
const openHome = async () => {
  await render(<App />);

  const input = await screen.findByTestId('pin-input');
  fireEvent.changeText(input, PIN);
  await waitFor(() => expect(screen.getByTestId('pin-input').props.value).toBe(PIN));

  fireEvent.press(screen.getByTestId('unlock-with-pin'));

  await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
};

describe('App — hardware back', () => {
  beforeEach(async () => {
    (global as any).__resetNativeMocks();
    backPresses.length = 0;

    jest.spyOn(BackHandler, 'addEventListener').mockImplementation(((
      _event: string,
      handler: () => boolean,
    ) => {
      backPresses.push(handler);
      return {
        remove: () => {
          const at = backPresses.indexOf(handler);
          if (at >= 0) backPresses.splice(at, 1);
        },
      };
    }) as typeof BackHandler.addEventListener);

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

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('returns to the hub instead of closing the app', async () => {
    await openHome();

    fireEvent.press(screen.getByTestId('subject-sync'));
    await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());

    // `true` means the app consumed the press. Before this commit nothing did,
    // and the OS closed the app from here.
    await waitFor(() => expect(pressBack()).toBe(true));
    await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
  });

  it('lands in the same place the on-screen button does', async () => {
    await openHome();

    fireEvent.press(screen.getByTestId('subject-today'));
    await waitFor(() => expect(screen.getByTestId('today-screen')).toBeTruthy());
    fireEvent.press(await screen.findByTestId('go-home'));
    const viaButton = await screen.findByTestId('activities-hub');

    fireEvent.press(screen.getByTestId('subject-today'));
    await waitFor(() => expect(screen.getByTestId('today-screen')).toBeTruthy());
    await waitFor(() => expect(pressBack()).toBe(true));
    const viaBack = await screen.findByTestId('activities-hub');

    // Two buttons that land in different places is how a route ends up
    // unreachable; these land in the same one (T5.3).
    expect(viaBack.props.testID).toBe(viaButton.props.testID);
  });

  it('still lets the OS close the app from the hub', async () => {
    await openHome();

    // The hub is where "back" means what the employee thinks it means. Consuming
    // it here would trap them in an app they cannot leave.
    expect(pressBack()).toBe(false);
    expect(screen.getByTestId('activities-hub')).toBeTruthy();
  });

  it('asks for confirmation before leaving a screen with an active draft', async () => {
    await openHome();

    fireEvent.press(screen.getByTestId('subject-birth'));
    await waitFor(() => expect(screen.getByTestId('birth-screen')).toBeTruthy());

    // Pick a dam to mark the screen dirty
    fireEvent.press(screen.getByTestId('dam-dam-1'));
    await waitFor(() => expect(screen.getByTestId('birth-step-2')).toBeTruthy());

    // Hardware back should trigger the discard prompt
    pressBack();
    await waitFor(() => expect(screen.getByTestId('app-discard-prompt')).toBeTruthy());
    expect(screen.queryByTestId('activities-hub')).toBeNull();

    // "Seguir aquí" dismisses the prompt and stays on the screen
    fireEvent.press(screen.getByTestId('keep-editing'));
    await waitFor(() => expect(screen.queryByTestId('app-discard-prompt')).toBeNull());
    expect(screen.getByTestId('birth-step-2')).toBeTruthy();

    // Hardware back again triggers prompt; pressing hardware back again dismisses it
    pressBack();
    await waitFor(() => expect(screen.getByTestId('app-discard-prompt')).toBeTruthy());
    pressBack();
    await waitFor(() => expect(screen.queryByTestId('app-discard-prompt')).toBeNull());
    expect(screen.getByTestId('birth-step-2')).toBeTruthy();

    // On-screen "Inicio" button also triggers the prompt
    fireEvent.press(screen.getByTestId('go-home'));
    await waitFor(() => expect(screen.getByTestId('app-discard-prompt')).toBeTruthy());

    // "Salir y descartar" confirms leaving and returns to the hub
    fireEvent.press(screen.getByTestId('discard-draft'));
    await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
  });
});

