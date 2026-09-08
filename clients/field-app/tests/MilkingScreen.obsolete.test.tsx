import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { Outbox } from '../src/services/outbox';
import { MilkingService } from '../src/services/milkingService';
import { MilkingScreen } from '../src/screens/MilkingScreen';

/**
 * T6.1, T6.2, T6.6: Revalidation upon confirm and draft preservation.
 * When sync changes animal fitness (e.g. disposed or sex changed) while the form is open,
 * submission is blocked with a clear notice and the operator's input (liters) is preserved.
 */
describe('MilkingScreen: obsolete animal handling and draft preservation', () => {
  let database: Database;
  let outbox: Outbox;
  let service: MilkingService;

  const speciesId = 'species-bovino';
  const candidates = [
    { animalId: 'cow-1', label: 'La Pinta', isWithheld: false, speciesIsMilkable: true, sex: 'Female' },
    { animalId: 'cow-2', label: 'La Negra', isWithheld: false, speciesIsMilkable: true, sex: 'Female' },
  ];

  beforeEach(async () => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-milking-obsolete-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    outbox = new Outbox(database);
    service = new MilkingService(database);

    await database.write(async () => {
      await database.get('species').create((row: any) => {
        row._raw.id = speciesId;
        row.name = 'Bovino';
        row.isMilkable = true;
        row.isDeleted = false;
      });
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-1';
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
      await database.get('animals').create((row: any) => {
        row._raw.id = 'cow-2';
        row.sex = 'Female';
        row.speciesId = speciesId;
        row.isDeleted = false;
        row.serverCreatedAt = Date.now();
      });
    });

    global.fetch = jest.fn(() => {
      throw new Error('MilkingScreen no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('informs when selected cow becomes obsolete during entry, disables confirm, and preserves liters (T6.1, T6.2 & T6.6)', async () => {
    const { rerender } = await render(
      <MilkingScreen
        service={service}
        database={database}
        candidates={candidates}
        recordedBy="tester@hato"
        initialAnimalId="cow-1"
      />,
    );

    const input = await screen.findByTestId('liters-input');
    await fireEvent.changeText(input, '14.5');

    // Simulate sync updating candidates: cow-1 is no longer eligible (disposed or male)
    const updatedCandidates = [
      { animalId: 'cow-2', label: 'La Negra', isWithheld: false, speciesIsMilkable: true, sex: 'Female' },
    ];
    await rerender(
      <MilkingScreen
        service={service}
        database={database}
        candidates={updatedCandidates}
        recordedBy="tester@hato"
      />,
    );

    // Warning notice appears
    expect(await screen.findByText(/ya no existe en el sistema/i)).toBeTruthy();

    // Confirm button is disabled
    const confirmButton = screen.getByTestId('confirm-milking');
    expect(confirmButton.props.accessibilityState?.disabled).toBe(true);

    // Draft is preserved: liters input still holds 14.5
    expect(screen.getByTestId('liters-input').props.value).toBe('14.5');

    // Operator can choose another animal without losing entered liters
    await fireEvent.press(screen.getByTestId('change-animal'));
    expect(await screen.findByTestId('cow-cow-2')).toBeTruthy();

    await fireEvent.press(screen.getByTestId('cow-cow-2'));

    // cow-2 is selected, warning gone, and liters 14.5 is preserved
    expect(await screen.findByText('La Negra')).toBeTruthy();
    expect(screen.queryByText(/ya no existe en el sistema/i)).toBeNull();
    expect(screen.getByTestId('liters-input').props.value).toBe('14.5');
  });
});
