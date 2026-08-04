require('./native-mocks');

// React Navigation's stack needs these two shims under Jest.
jest.mock('react-native-gesture-handler', () => ({}));

// The first test in each suite pays for LokiJSAdapter + RN Testing Library startup,
// which reliably exceeds Jest's 5000ms default on GitHub Actions' shared runners even
// though it's fast on a beefier local machine.
jest.setTimeout(20000);
