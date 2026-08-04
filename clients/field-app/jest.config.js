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
      preset: 'react-native',
      testMatch: ['<rootDir>/tests/**/*.test.tsx'],
      setupFiles: ['<rootDir>/tests/setup/ui-setup.js'],
      transformIgnorePatterns: [
        'node_modules/(?!(jest-)?react-native|@react-native|expo(nent)?|@expo|@nozbe|@testing-library)',
      ],
      // The first test in each ui suite pays for LokiJSAdapter + RN Testing Library
      // startup, which reliably exceeds Jest's 5000ms default on GitHub Actions'
      // shared runners even though it's fast on a beefier local machine.
      testTimeout: 20000,
    },
  ],
};
