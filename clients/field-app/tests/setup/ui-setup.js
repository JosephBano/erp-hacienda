require('./native-mocks');

// react-navigation/react-native-gesture-handler shims removed (ADR-0011): the navigation
// stack lives entirely on useState<Tab> in App.tsx, so neither mock is needed any more.

// React 19 + @testing-library/react-native@14 want this global set so fireEvent et al.
// schedule inside React's act() boundary automatically. Without it the test renderer
// logs "The current testing environment is not configured to support act(...)" and
// fireEvent calls may run before the first paint of the just-rendered tree.
globalThis.IS_REACT_ACT_ENVIRONMENT = true;

// The per-test cleanup hook lives in setupFilesAfterEach (ui-setup-aftereach.js):
// setupFiles runs before Jest's globals exist, so `afterEach` is undefined here.

// The first test in each suite pays for LokiJSAdapter + RN Testing Library startup,
// which reliably exceeds Jest's 5000ms default on GitHub Actions' shared runners even
// though it's fast on a beefier local machine.
jest.setTimeout(20000);
