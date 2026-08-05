import { Database } from '@nozbe/watermelondb';
import SQLiteAdapter from '@nozbe/watermelondb/adapters/sqlite';

import { migrations } from './migrations';
import { modelClasses } from './models';
import { schema } from './schema';

/**
 * The device database: SQLite through WatermelonDB.
 *
 * `jsi: false` because the JSI adapter is not auto-linked for our WatermelonDB 0.28 +
 * Expo SDK 56 + RN 0.85 setup (WatermelonDB's react-native.config.js only ships the async
 * Android module; the JSI module needs the @morrowdigital/watermelondb-expo-plugin or an
 * explicit WatermelonDBJSIPackage registration). Passing `jsi: true` here without that
 * scaffolding is a silent no-op — makeDispatcher/index.native.js:124-132 falls back to
 * the async adapter with a warning. ADR-0012 defers activating the real JSI build until
 * we have measured data that justifies it.
 *
 * `onSetUpError` matters more than it looks. If the local database cannot be opened, the
 * app must say so loudly rather than start with an empty one — silently continuing would
 * present an employee with a blank herd and an outbox that appears empty while a week of
 * unsynced records sits unreachable on disk.
 */
export function createDatabase(onSetUpError?: (error: Error) => void): Database {
  const adapter = new SQLiteAdapter({
    schema,
    migrations,
    jsi: false,
    onSetUpError: (error) => {
      onSetUpError?.(error);
    },
  });

  return new Database({ adapter, modelClasses });
}
