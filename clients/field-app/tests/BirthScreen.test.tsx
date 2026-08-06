import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { BirthService } from '../src/services/birthService';
import { BirthScreen } from '../src/screens/BirthScreen';

/**
 * Smoke-level coverage for the Birth screen. The interaction-heavy paths are intentionally
 * covered by the logic suites against the real WatermelonDB schema; this file exists
 * specifically to lock the empty-state copy that an employee sees on first launch (no
 * dams yet because the herd has not been synced down).
 */
describe('BirthScreen', () => {
  let database: Database;
  let service: BirthService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-birth-screen-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    service = new BirthService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('points the employee to Sync when there are no dams to choose from', async () => {
    await render(<BirthScreen service={service} dams={[]} sires={[]} />);

    expect(await screen.findByTestId('dam-list-empty')).toBeTruthy();
    expect(screen.queryByTestId('dam-anything')).toBeNull();
  });

  it('renders the dam picker when at least one dam is available', async () => {
    const dams = [{ animalId: 'dam-1', label: 'La Pinta' }];
    await render(<BirthScreen service={service} dams={dams} sires={[]} />);

    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();
    expect(screen.queryByTestId('dam-list-empty')).toBeNull();
  });
});
