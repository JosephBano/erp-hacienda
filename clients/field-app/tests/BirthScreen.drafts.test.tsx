import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { BirthService } from '../src/services/birthService';
import { BirthScreen } from '../src/screens/BirthScreen';
import { DraftGuardProvider } from '../src/ui/draftGuard';
import type { PregnantDam } from '../src/services/herdQueries';

/**
 * feature-0006 commit 5 — criterion 4 ("volver de un paso conserva sexo, arete,
 * peso …") and D3 ("abandonarlo con cambios advierte antes de descartarlos")
 * on the wizard where losing the entry costs the most.
 *
 * A birth is not re-typable from memory: the employee is in the pen with the
 * calves, and the sex/arete/peso of each one is what they are looking at. A
 * step back that emptied the list would send them out to count again.
 */
const damA: PregnantDam = {
  animalId: 'dam-1',
  label: 'La Pinta',
  pregnancyId: 'preg-1',
  sireLabel: 'Padre: Inseminación artificial',
  expectedBirthDate: '2026-09-01',
};

const damB: PregnantDam = {
  animalId: 'dam-2',
  label: 'Carlota',
  pregnancyId: 'preg-2',
  sireLabel: 'Padre: Don Juan',
  expectedBirthDate: '2026-10-08',
};

const press = async (testID: string) => {
  await act(async () => {
    fireEvent.press(screen.getByTestId(testID));
  });
};

const type = async (testID: string, text: string) => {
  await act(async () => {
    fireEvent.changeText(screen.getByTestId(testID), text);
  });
};

/** What the row actually shows: these inputs are uncontrolled, seeded on mount. */
const shownText = (testID: string): string =>
  (screen.getByTestId(testID).props as { defaultValue?: string; value?: string }).defaultValue ??
  (screen.getByTestId(testID).props as { value?: string }).value ??
  '';

describe('BirthScreen — borradores y regreso entre pasos', () => {
  let database: Database;
  let service: BirthService;

  beforeEach(() => {
    const adapter = new LokiJSAdapter({
      schema,
      migrations,
      useWebWorker: false,
      useIncrementalIndexedDB: false,
      dbName: `hato-birth-drafts-${Math.random()}`,
    });

    database = new Database({ adapter: adapter as never, modelClasses });
    service = new BirthService(database);

    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const renderScreen = async (
    props: Partial<React.ComponentProps<typeof BirthScreen>> = {},
    onDirtyChange: (dirty: boolean) => void = () => undefined,
  ) =>
    render(
      <DraftGuardProvider onDirtyChange={onDirtyChange}>
        <BirthScreen service={service} dams={[damA, damB]} {...props} />
      </DraftGuardProvider>,
    );

  /** Walks to step 3 with two calves fully described. */
  const loadTwoCalves = async () => {
    await press('dam-dam-1');
    await press('next-step-2');
    await press('add-female');
    await press('add-male');
    await type('offspring-tag-0', 'A-101');
    await type('offspring-weight-0', '32');
    await type('offspring-tag-1', 'B-202');
    await type('offspring-weight-1', '35.5');
  };

  const expectTwoCalvesIntact = () => {
    expect(screen.getByTestId('offspring-count').props.children).toBe('Crías: 2');
    expect(screen.getByText('1. Hembra')).toBeTruthy();
    expect(screen.getByText('2. Macho')).toBeTruthy();
    expect(shownText('offspring-tag-0')).toBe('A-101');
    expect(shownText('offspring-weight-0')).toBe('32');
    expect(shownText('offspring-tag-1')).toBe('B-202');
    expect(shownText('offspring-weight-1')).toBe('35.5');
  };

  it('conserva sexo, arete y peso de cada cría al ir del paso 3 al 2 y volver', async () => {
    await renderScreen();
    await loadTwoCalves();

    await press('prev-step-3');
    expect(screen.getByTestId('birth-step-2')).toBeTruthy();
    await press('next-step-2');

    // The six values the employee read off the calves, still there.
    expectTwoCalvesIntact();
  });

  it('conserva las crías al ir del paso 4 al 3', async () => {
    await renderScreen();
    await loadTwoCalves();

    await press('next-step-3');
    expect(screen.getByTestId('confirm-birth')).toBeTruthy();
    await press('prev-step-4');

    expectTwoCalvesIntact();
  });

  it('conserva la fecha escrita al volver del paso 3 al 2', async () => {
    await renderScreen();

    await press('dam-dam-1');
    await type('birth-date-input', '2026-09-02');
    await press('next-step-2');
    await press('prev-step-3');

    expect(screen.getByTestId('birth-date-input').props.value).toBe('2026-09-02');
  });

  it('no marca borrador mientras el asistente sigue en el paso 1 vacío', async () => {
    const dirty: boolean[] = [];
    await renderScreen({}, (next) => dirty.push(next));

    // Warning on a screen where nothing has been entered teaches the employee to
    // tap "Salir y descartar" without reading it.
    expect(dirty.every((value) => value === false)).toBe(true);
  });

  it('marca borrador en cuanto hay madre elegida', async () => {
    const dirty: boolean[] = [];
    await renderScreen({}, (next) => dirty.push(next));

    await press('dam-dam-1');

    expect(dirty[dirty.length - 1]).toBe(true);
  });

  it('deja de marcar borrador tras registrar el parto', async () => {
    const dirty: boolean[] = [];
    await renderScreen({ onRecorded: () => undefined }, (next) => dirty.push(next));

    await loadTwoCalves();
    await press('next-step-3');
    await press('confirm-birth');

    // The wizard resets itself on success: nothing is pending any more, so the
    // next exit must not ask.
    expect(dirty[dirty.length - 1]).toBe(false);
  });

  it('un refresco de las madres no reinicia el paso ni borra las crías', async () => {
    const view = await renderScreen();
    await loadTwoCalves();

    // App.refresh() replaces `dams` after every write and after each sync.
    await act(async () => {
      view.rerender(
        <DraftGuardProvider onDirtyChange={() => undefined}>
          <BirthScreen service={service} dams={[{ ...damA }, { ...damB }, { ...damB, animalId: 'dam-3' }]} />
        </DraftGuardProvider>,
      );
    });

    expectTwoCalvesIntact();
  });
});
