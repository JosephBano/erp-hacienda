// Runs after each test in the UI project. RTL 14's render()/cleanup() pair is async,
// and without an explicit await here Jest races the next test against the previous
// renderer's tear-down — leaving `screen` pointing at a stale tree.
const { cleanup } = require('@testing-library/react-native');

afterEach(async () => {
  await cleanup();
});