require('./native-mocks');

const mockAppState = { currentState: 'active', listeners: new Set() };

jest.mock(
  'react-native',
  () => ({
    AppState: {
      get currentState() {
        return mockAppState.currentState;
      },
      addEventListener(type, listener) {
        if (type === 'change') {
          mockAppState.listeners.add(listener);
        }
        return {
          remove: () => {
            mockAppState.listeners.delete(listener);
          },
        };
      },
    },
  }),
  { virtual: true }
);

global.__setAppState = (state) => {
  mockAppState.currentState = state;
  for (const listener of mockAppState.listeners) {
    listener(state);
  }
};

const originalReset = global.__resetNativeMocks;
global.__resetNativeMocks = () => {
  originalReset?.();
  mockAppState.currentState = 'active';
  mockAppState.listeners.clear();
};

