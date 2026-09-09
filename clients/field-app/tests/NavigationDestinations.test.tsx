import React from 'react';
import * as SecureStore from 'expo-secure-store';
import { BackHandler } from 'react-native';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

/**
 * feature-0010 (field-app-redesign) commit 2 — navigation destinations & architecture.
 *
 * Verifies:
 *  - T2.1: The four canonical destinations: Inicio, Animales, Lotes, Actividad (symbol + text).
 *  - T2.2: Global header with sync pill accessible from any destination.
 *  - T2.3: Secondary settings modal with active operator, theme switching and sign out.
 *  - T2.5 & T2.6: Hardware back and on-screen back coherence; draft guard safety.
 */

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

jest.mock('../src/database', () => ({ createDatabase: () => ({}) }));

jest.mock('../src/services/herdQueries', () => {
  const actual = jest.requireActual('../src/services/herdQueries');
  return {
    ...actual,
    loadHerd: async () => [
      {
        animalId: 'animal-1',
        label: '1042',
        tag: '1042',
        sex: 'female',
        groupName: 'Lote 1',
      },
    ],
    loadActiveHerd: async () => [
      {
        animalId: 'animal-1',
        label: '1042',
        tag: '1042',
        sex: 'female',
        groupName: 'Lote 1',
      },
    ],
    loadMilkingCandidates: async () => [],
    loadGroups: async () => [
      { groupId: 'lot-1', label: 'Engorde 1', trackingMode: 'headcount' },
    ],
    loadTreatmentProducts: async () => [],
    loadMedications: async () => [],
    loadMortalityCauses: async () => [],
    loadFeedItems: async () => [],
    loadPregnantDams: async () => [
      {
        animalId: 'dam-1',
        label: 'La Pinta',
        pregnancyId: 'preg-1',
        sireLabel: 'Padre: IA',
        expectedBirthDate: '2026-09-01',
      },
    ],
    loadBreeds: async () => [],
    loadCategories: async () => [],
  };
});

let mockPendingCount = 0;

jest.mock('../src/services/outbox', () => ({
  Outbox: class {
    async stats() {
      return { pending: mockPendingCount, synced: 0, rejected: 0, cancelled: 0 };
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
const SALT = 'salt-for-navigation-test';

const pinHash = () =>
  require('crypto').createHash('sha256').update(`${SALT}:${PIN}`).digest('hex') as string;

const backPresses: (() => boolean)[] = [];
const pressBack = () => {
  const handler = backPresses[backPresses.length - 1];
  if (!handler) throw new Error('App registered no hardware back handler.');
  return handler();
};

const openHome = async () => {
  await render(<App />);

  const input = await screen.findByTestId('pin-input');
  fireEvent.changeText(input, PIN);
  await waitFor(() => expect(screen.getByTestId('pin-input').props.value).toBe(PIN));

  fireEvent.press(screen.getByTestId('unlock-with-pin'));

  await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
};

describe('Navigation Destinations (feature-0010 commit 2)', () => {
  beforeEach(async () => {
    (global as any).__resetNativeMocks();
    backPresses.length = 0;
    mockPendingCount = 0;

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
        userId: 'user-field-1',
        fullName: 'Juan Operador',
        email: 'juan@hato.test',
        token: 'token-xyz',
        refreshToken: 'refresh-xyz',
        expiresAt: '2099-01-01T00:00:00.000Z',
        roles: ['operator'],
        permissions: [],
        pinHash: pinHash(),
        pinSalt: SALT,
      }),
    );

    global.fetch = jest.fn(() => {
      throw new Error('No se debe llamar a la red en pruebas de navegación local.');
    }) as unknown as typeof fetch;
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('T2.1: The four canonical destinations', () => {
    it('displays the fixed bottom navigation bar with the 4 destinations recognized by text and symbol', async () => {
      await openHome();

      expect(await screen.findByTestId('bottom-nav')).toBeTruthy();

      const homeTab = screen.getByTestId('nav-home');
      expect(homeTab).toBeTruthy();
      expect(homeTab.props.accessibilityLabel).toBe('Inicio');

      const animalsTab = screen.getByTestId('nav-animals');
      expect(animalsTab).toBeTruthy();
      expect(animalsTab.props.accessibilityLabel).toBe('Animales');

      const lotsTab = screen.getByTestId('nav-lots');
      expect(lotsTab).toBeTruthy();
      expect(lotsTab.props.accessibilityLabel).toBe('Lotes');

      const activityTab = screen.getByTestId('nav-activity');
      expect(activityTab).toBeTruthy();
      expect(activityTab.props.accessibilityLabel).toBe('Actividad');

      // Verify symbols & labels exist
      expect(screen.getByText('🏠')).toBeTruthy();
      expect(screen.getByText('Inicio')).toBeTruthy();
      expect(screen.getByText('🏷️')).toBeTruthy();
      expect(screen.getByText('Animales')).toBeTruthy();
      expect(screen.getByText('👥')).toBeTruthy();
      expect(screen.getByText('Lotes')).toBeTruthy();
      expect(screen.getByText('📋')).toBeTruthy();
      expect(screen.getByText('Actividad')).toBeTruthy();
    });

    it('navigates cleanly between the four destinations', async () => {
      await openHome();

      // Tap Animales
      fireEvent.press(screen.getByTestId('nav-animals'));
      await waitFor(() => expect(screen.getByTestId('animal-subject-screen')).toBeTruthy());

      // Tap Lotes
      fireEvent.press(screen.getByTestId('nav-lots'));
      await waitFor(() => expect(screen.getByTestId('lot-subject-screen')).toBeTruthy());

      // Tap Actividad
      fireEvent.press(screen.getByTestId('nav-activity'));
      await waitFor(() => expect(screen.getByTestId('today-screen')).toBeTruthy());

      // Tap Inicio
      fireEvent.press(screen.getByTestId('nav-home'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
    });
  });

  describe('T2.2: Global header and sync pill access from everywhere', () => {
    it('shows the global header and sync pill showing "Al día" when pending is 0', async () => {
      mockPendingCount = 0;
      await openHome();

      expect(await screen.findByTestId('global-header')).toBeTruthy();
      const pill = screen.getByTestId('global-sync-pill');
      expect(pill).toBeTruthy();
      expect(screen.getByText(/✓ Al día/)).toBeTruthy();
    });

    it('opens the sync status screen from any of the destinations when the sync pill is tapped', async () => {
      await openHome();

      // From Inicio
      fireEvent.press(screen.getByTestId('global-sync-pill'));
      await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('go-home'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());

      // From Animales
      fireEvent.press(screen.getByTestId('nav-animals'));
      await waitFor(() => expect(screen.getByTestId('animal-subject-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('global-sync-pill'));
      await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('go-home'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());

      // From Lotes
      fireEvent.press(screen.getByTestId('nav-lots'));
      await waitFor(() => expect(screen.getByTestId('lot-subject-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('global-sync-pill'));
      await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('go-home'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());

      // From Actividad
      fireEvent.press(screen.getByTestId('nav-activity'));
      await waitFor(() => expect(screen.getByTestId('today-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('global-sync-pill'));
      await waitFor(() => expect(screen.getByTestId('sync-status-screen')).toBeTruthy());
      fireEvent.press(screen.getByTestId('go-home'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
    });
  });

  describe('T2.3: Secondary settings and session', () => {
    it('opens settings modal with operator information, theme switcher, and sign-out', async () => {
      await openHome();

      fireEvent.press(screen.getByTestId('header-settings-button'));
      await waitFor(() => expect(screen.getByTestId('settings-modal')).toBeTruthy());

      // Operator info
      expect(screen.getByTestId('settings-user-info')).toBeTruthy();
      expect(screen.getByText('Juan Operador')).toBeTruthy();
      expect(screen.getByText('juan@hato.test')).toBeTruthy();

      // Theme options
      expect(screen.getByTestId('theme-mode-system')).toBeTruthy();
      expect(screen.getByTestId('theme-mode-light')).toBeTruthy();
      expect(screen.getByTestId('theme-mode-dark')).toBeTruthy();

      // Close modal
      fireEvent.press(screen.getByTestId('settings-close-button'));
      await waitFor(() => expect(screen.queryByTestId('settings-modal')).toBeNull());
    });

    it('allows closing the settings modal with hardware back without leaving the current screen', async () => {
      await openHome();

      fireEvent.press(screen.getByTestId('nav-animals'));
      await waitFor(() => expect(screen.getByTestId('animal-subject-screen')).toBeTruthy());

      // Open settings
      fireEvent.press(screen.getByTestId('header-settings-button'));
      await waitFor(() => expect(screen.getByTestId('settings-modal')).toBeTruthy());

      // Hardware back closes modal and leaves user on Animales
      expect(pressBack()).toBe(true);
      await waitFor(() => expect(screen.queryByTestId('settings-modal')).toBeNull());
      expect(screen.getByTestId('animal-subject-screen')).toBeTruthy();
    });

    it('signs out and returns to login screen', async () => {
      await openHome();

      fireEvent.press(screen.getByTestId('header-settings-button'));
      await waitFor(() => expect(screen.getByTestId('settings-modal')).toBeTruthy());

      fireEvent.press(screen.getByTestId('settings-sign-out'));
      await waitFor(() => expect(screen.getByTestId('login-screen')).toBeTruthy());
    });
  });

  describe('T2.6: Hardware back and navigation coherence', () => {
    it('returns to Inicio when pressing hardware back from Animales, Lotes, or Actividad', async () => {
      await openHome();

      // From Animales
      fireEvent.press(screen.getByTestId('nav-animals'));
      await waitFor(() => expect(screen.getByTestId('animal-subject-screen')).toBeTruthy());
      expect(pressBack()).toBe(true);
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());

      // From Lotes
      fireEvent.press(screen.getByTestId('nav-lots'));
      await waitFor(() => expect(screen.getByTestId('lot-subject-screen')).toBeTruthy());
      expect(pressBack()).toBe(true);
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());

      // From Actividad
      fireEvent.press(screen.getByTestId('nav-activity'));
      await waitFor(() => expect(screen.getByTestId('today-screen')).toBeTruthy());
      expect(pressBack()).toBe(true);
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
    });

    it('returns false on Inicio to let Android OS exit the app', async () => {
      await openHome();
      expect(pressBack()).toBe(false);
      expect(screen.getByTestId('activities-hub')).toBeTruthy();
    });

    it('intercepts tab switching when a draft is dirty', async () => {
      await openHome();

      fireEvent.press(screen.getByTestId('subject-birth'));
      await waitFor(() => expect(screen.getByTestId('birth-screen')).toBeTruthy());

      // Dirty the draft by picking a dam
      fireEvent.press(screen.getByTestId('dam-dam-1'));
      await waitFor(() => expect(screen.getByTestId('birth-step-2')).toBeTruthy());

      // Attempting to tap on-screen Inicio triggers discard prompt
      fireEvent.press(screen.getByTestId('go-home'));
      await waitFor(() => expect(screen.getByTestId('app-discard-prompt')).toBeTruthy());

      // Keep editing stays on step 2
      fireEvent.press(screen.getByTestId('keep-editing'));
      await waitFor(() => expect(screen.queryByTestId('app-discard-prompt')).toBeNull());
      expect(screen.getByTestId('birth-step-2')).toBeTruthy();

      // Hardware back triggers discard prompt
      expect(pressBack()).toBe(true);
      await waitFor(() => expect(screen.getByTestId('app-discard-prompt')).toBeTruthy());

      // Discarding returns to Inicio
      fireEvent.press(screen.getByTestId('discard-draft'));
      await waitFor(() => expect(screen.getByTestId('activities-hub')).toBeTruthy());
    });
  });
});
