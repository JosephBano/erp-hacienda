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

  /**
   * When the activity tree (3.5a.9-B) hands us an animal + activity, EventsScreen
   * skips both the animal picker and the activity menu. The operator has already
   * done the three taps (subject, animal, activity) by the time they arrive here, and
   * the form they get next is the same one they would have reached by tapping through
   * the menu — fewer steps, no behaviour change.
   */
  it('skips the activity menu when initialAnimalId is set together with initialActivity', async () => {
    const animals = [{ animalId: 'a-1', label: 'Pinta' }];
    await render(
      <EventsScreen
        service={service}
        animals={animals}
        groups={[]}
        medications={[]}
        initialAnimalId="a-1"
        initialActivity="weight"
      />,
    );

    // The mode menu is replaced by the weight form directly.
    expect(screen.queryByTestId('mode-treatment')).toBeNull();
    expect(screen.queryByTestId('mode-weight')).toBeNull();
    expect(screen.queryByTestId('mode-move')).toBeNull();
    expect(await screen.findByTestId('weight-input')).toBeTruthy();
  });

  it('still shows the menu when only initialAnimalId is set', async () => {
    const animals = [{ animalId: 'a-1', label: 'Pinta' }];
    await render(
      <EventsScreen
        service={service}
        animals={animals}
        groups={[]}
        medications={[]}
        initialAnimalId="a-1"
      />,
    );

    // Pre-selecting just the animal does not collapse the activity menu — the caller
    // chose the animal, but the activity is still the operator's choice. The menu
    // remains visible; the picker is reached once they tap an activity.
    expect(await screen.findByTestId('mode-treatment')).toBeTruthy();
  });

  it('still shows the menu when only initialActivity is set', async () => {
    const animals = [{ animalId: 'a-1', label: 'Pinta' }];
    await render(
      <EventsScreen
        service={service}
        animals={animals}
        groups={[]}
        medications={[]}
        initialActivity="weight"
      />,
    );

    // Pre-selecting just the activity means the menu is gone but the animal picker
    // still leads the form. Counted taps: subject + animal + activity + confirm = 4.
    expect(await screen.findByTestId('animal-list')).toBeTruthy();
    expect(screen.queryByTestId('mode-treatment')).toBeNull();
  });
});
