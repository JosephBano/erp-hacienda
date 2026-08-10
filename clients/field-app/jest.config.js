// Two projects, because the two kinds of test need different worlds.
//
// "logic" covers the local database, the outbox and the field services. It runs under
// jsdom because WatermelonDB's LokiJS adapter — the in-memory backend that lets tests
// exercise the *real* schema, models and queries rather than a hand-rolled fake — expects
// a browser-ish global.
//
// "ui" renders the actual screens through React Native Testing Library, using React
// Native's own Jest preset so the native module shims are in place.
module.exports = {
  projects: [
    {
      displayName: 'logic',
      testEnvironment: 'jsdom',
      testMatch: ['<rootDir>/tests/**/*.test.ts'],
      setupFiles: ['<rootDir>/tests/setup/logic-setup.js'],
      transformIgnorePatterns: [
        'node_modules/(?!(jest-)?react-native|@react-native|expo(nent)?|@expo|@nozbe)',
      ],
    },
    {
      displayName: 'ui',
      preset: '@react-native/jest-preset',
      testMatch: ['<rootDir>/tests/**/*.test.tsx'],
      setupFiles: ['<rootDir>/tests/setup/ui-setup.js'],
      // Was `setupFilesAfterEach` (not a real Jest option — silently ignored
      // with a "Unknown option" warning every run, and the RTL cleanup()
      // hook in ui-setup-aftereach.js never executed as a result). Found
      // while chasing cross-test render corruption in 3.5a.2-C's new screen
      // tests: without this hook, RTL 14's async cleanup() races the next
      // test's render(), leaving `screen` pointing at a stale tree exactly
      // the way the file's own comment describes.
      setupFilesAfterEnv: ['<rootDir>/tests/setup/ui-setup-aftereach.js'],
      transformIgnorePatterns: [
        'node_modules/(?!(jest-)?react-native|@react-native|expo(nent)?|@expo|@nozbe|@testing-library)',
      ],
    },
  ],
};
