require('./native-mocks');

// React Navigation's stack needs these two shims under Jest.
jest.mock('react-native-gesture-handler', () => ({}));
