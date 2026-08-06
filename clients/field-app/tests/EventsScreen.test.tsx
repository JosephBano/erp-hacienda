import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { EventService } from '../src/services/eventService';
import { EventsScreen } from '../src/screens/EventsScreen';

/**
 * Smoke-level coverage for the Events screen. The interaction-heavy paths (Treatment,
 * Weighting, Move) are intentionally skipped elsewhere to dodge the SDK 51 -> 56 + RTL 14
 * race; the goal here is to lock down the empty-state copy that the field uses to
 * diagnose "why does the screen look broken on a fresh install".
 */
describe('EventsScreen', () => {
  let database: Database;
  let service: EventService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-events-screen-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    service = new EventService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de eventos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('starts at the menu with the three event options', async () => {
    await render(
      <EventsScreen
        service={service}
        animals={[]}
        groups={[]}
        medications={[]}
      />,
    );

    expect(await screen.findByTestId('mode-treatment')).toBeTruthy();
    expect(screen.getByTestId('mode-weight')).toBeTruthy();
    expect(screen.getByTestId('mode-move')).toBeTruthy();
    expect(screen.queryByTestId('events-animal-empty')).toBeNull();
  });
});
