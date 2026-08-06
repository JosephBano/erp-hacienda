// Stand-ins for the Expo native modules. They are deliberately *behavioural* rather than
// assertion targets: expo-secure-store gets a real in-memory key/value store so a test can
// simulate "close the app and open it again", and expo-crypto gets real randomness and a
// real SHA-256, so the code under test is exercised, not the mock.
const mockNodeCrypto = require('crypto');

const mockSecureStore = new Map();

jest.mock('expo-secure-store', () => ({
  async setItemAsync(key, value) {
    mockSecureStore.set(key, value);
  },
  async getItemAsync(key) {
    return mockSecureStore.has(key) ? mockSecureStore.get(key) : null;
  },
  async deleteItemAsync(key) {
    mockSecureStore.delete(key);
  },
  async isAvailableAsync() {
    return true;
  },
}));

jest.mock('expo-crypto', () => ({
  CryptoDigestAlgorithm: { SHA256: 'SHA-256' },
  randomUUID: () => mockNodeCrypto.randomUUID(),
  getRandomBytes: (count) => new Uint8Array(mockNodeCrypto.randomBytes(count)),
  async getRandomBytesAsync(count) {
    return new Uint8Array(mockNodeCrypto.randomBytes(count));
  },
  async digestStringAsync(_algorithm, data) {
    return mockNodeCrypto.createHash('sha256').update(data).digest('hex');
  },
}));

// Connectivity is driven explicitly by the tests through this handle, so "the signal came
// back" is something a test can stage instead of wait for.
const mockNetState = { isConnected: true, listeners: new Set() };

jest.mock('@react-native-community/netinfo', () => ({
  async fetch() {
    return { isConnected: mockNetState.isConnected, isInternetReachable: mockNetState.isConnected };
  },
  addEventListener(listener) {
    mockNetState.listeners.add(listener);
    return () => mockNetState.listeners.delete(listener);
  },
}));

global.__setNetworkConnected = (connected) => {
  mockNetState.isConnected = connected;
  for (const listener of mockNetState.listeners) {
    listener({ isConnected: connected, isInternetReachable: connected });
  }
};

global.__resetNativeMocks = () => {
  mockSecureStore.clear();
  mockNetState.isConnected = true;
  mockNetState.listeners.clear();
};
