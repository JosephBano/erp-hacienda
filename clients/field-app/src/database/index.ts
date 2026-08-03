import { Database } from '@nozbe/watermelondb';
import SQLiteAdapter from '@nozbe/watermelondb/adapters/sqlite';

import { migrations } from './migrations';
import { modelClasses } from './models';
import { schema } from './schema';

/**
 * The device database: SQLite through WatermelonDB's JSI adapter.
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
    jsi: true,
    onSetUpError: (error) => {
      onSetUpError?.(error);
    },
  });

  return new Database({ adapter, modelClasses });
}
