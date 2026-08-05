require('./native-mocks');

// react-navigation/react-native-gesture-handler shims removed (ADR-0011): the navigation
// stack lives entirely on useState<Tab> in App.tsx, so neither mock is needed any more.

// The first test in each suite pays for LokiJSAdapter + RN Testing Library startup,
// which reliably exceeds Jest's 5000ms default on GitHub Actions' shared runners even
// though it's fast on a beefier local machine.
jest.setTimeout(20000);
