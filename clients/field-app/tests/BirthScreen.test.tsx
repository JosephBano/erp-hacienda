import React from 'react';
import { Database } from '@nozbe/watermelondb';
import LokiJSAdapter from '@nozbe/watermelondb/adapters/lokijs';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { schema } from '../src/database/schema';
import { migrations } from '../src/database/migrations';
import { modelClasses } from '../src/database/models';
import { BirthService } from '../src/services/birthService';
import { BirthScreen } from '../src/screens/BirthScreen';
import type { PregnantDam } from '../src/services/herdQueries';

const sampleDam: PregnantDam = {
  animalId: 'dam-1',
  label: 'La Pinta',
  pregnancyId: 'preg-1',
  sireLabel: 'Padre: Inseminación artificial',
  expectedBirthDate: '2026-09-01',
};

const sampleNaturalDam: PregnantDam = {
  animalId: 'dam-2',
  label: 'Carlota',
  pregnancyId: 'preg-2',
  sireLabel: 'Padre: Don Juan',
  expectedBirthDate: '2026-10-08',
};

describe('BirthScreen — Step 1 & Empty State', () => {
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

  it('T5.12: points the employee to admin panel when there are no pregnant dams', async () => {
    await render(<BirthScreen service={service} dams={[]} />);

    expect(await screen.findByTestId('dam-list-empty')).toBeTruthy();
    expect(screen.getByText('No hay hembras con preñez activa')).toBeTruthy();
    expect(screen.getByText(/Solo aparecen hembras con preñez activa confirmada/)).toBeTruthy();
    expect(screen.queryByTestId('dam-anything')).toBeNull();
  });

  it('T5.12: renders pregnant dam list with expected birth dates', async () => {
    await render(<BirthScreen service={service} dams={[sampleDam, sampleNaturalDam]} />);

    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();
    expect(screen.getByTestId('dam-dam-2')).toBeTruthy();
    expect(screen.getByText(/La Pinta · FPP: 2026-09-01/)).toBeTruthy();
    expect(screen.getByText(/Carlota · FPP: 2026-10-08/)).toBeTruthy();
    expect(screen.queryByTestId('dam-list-empty')).toBeNull();
  });
});

describe('BirthScreen — Wizard flow and Calf management', () => {
  let recordBirth: jest.Mock;

  const buildService = () => {
    recordBirth = jest.fn().mockResolvedValue({
      clientOperationId: 'op-1',
      damId: 'dam-1',
      birthDate: '2026-08-17',
      offspringCount: 0,
    });
    return { recordBirth } as unknown as BirthService;
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de partos no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  const pickDam = async (damId = 'dam-1') => {
    await act(async () => {
      fireEvent.press(await screen.findByTestId(`dam-${damId}`));
    });
  };

  const goToStep3 = async () => {
    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-2'));
    });
  };

  const goToStep4 = async () => {
    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-3'));
    });
  };

  const tapAdd = async (kind: 'male' | 'female') => {
    await act(async () => {
      fireEvent.press(screen.getByTestId(kind === 'male' ? 'add-male' : 'add-female'));
    });
  };

  const tap = async (testId: string) => {
    await act(async () => {
      fireEvent.press(screen.getByTestId(testId));
    });
  };

  it('T5.13: does NOT have any sire-* testID anywhere in the wizard', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    // Step 1
    expect(screen.queryAllByTestId(/^sire-/)).toHaveLength(0);

    // Step 2
    await pickDam();
    expect(screen.queryAllByTestId(/^sire-/)).toHaveLength(0);
    expect(screen.getByText('Padre: Inseminación artificial')).toBeTruthy();

    // Step 3
    await goToStep3();
    expect(screen.queryAllByTestId(/^sire-/)).toHaveLength(0);

    // Step 4
    await tapAdd('female');
    await goToStep4();
    expect(screen.queryAllByTestId(/^sire-/)).toHaveLength(0);
    expect(screen.getByText('Padre: Inseminación artificial')).toBeTruthy();
  });

  it('T5.14: allows adding, removing, toggling sex, entering tag and weight', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    await pickDam();
    await goToStep3();

    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('male');
    await tapAdd('female');
    await tapAdd('female');

    // Remove third calf (index 2)
    await tap('remove-offspring-2');
    expect(screen.getByText('Crías: 4')).toBeTruthy();

    // Toggle sex of first calf from M to F
    await tap('toggle-offspring-0');

    // Edit tags and weights
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-tag-0'), 'CRIA-01');
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), '1.45');
      fireEvent.changeText(screen.getByTestId('offspring-tag-1'), 'CRIA-02');
      fireEvent.changeText(screen.getByTestId('offspring-weight-1'), '1.60');
    });

    await goToStep4();
    await tap('confirm-birth');

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    expect(recordBirth.mock.calls[0][0].offspring).toEqual([
      { sex: 'F', farmTag: 'CRIA-01', birthWeightKg: 1.45 },
      { sex: 'M', farmTag: 'CRIA-02', birthWeightKg: 1.6 },
      { sex: 'F', farmTag: undefined, birthWeightKg: undefined },
      { sex: 'F', farmTag: undefined, birthWeightKg: undefined },
    ]);
  });

  it('T5.15: rejects non-positive weight values', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    await pickDam();
    await goToStep3();
    await tapAdd('female');

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), '0');
    });
    expect(screen.getByText('El peso al nacer debe ser un valor positivo (en kilogramos).')).toBeTruthy();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), '-1.5');
    });
    expect(screen.getByText('El peso al nacer debe ser un valor positivo (en kilogramos).')).toBeTruthy();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), 'abc');
    });
    expect(screen.getByText('El peso al nacer debe ser un valor positivo (en kilogramos).')).toBeTruthy();

    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), '2.3');
    });
    expect(screen.queryByText('El peso al nacer debe ser un valor positivo (en kilogramos).')).toBeNull();
  });

  it('T5.16: going back and forth between steps preserves offspring and inputs', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    await pickDam();
    // Step 2: change difficulty
    await tap('difficulty-assisted');
    await goToStep3();

    // Step 3: add 3 calves
    await tapAdd('male');
    await tapAdd('female');
    await tapAdd('female');
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-tag-0'), 'LECHON-A');
      fireEvent.changeText(screen.getByTestId('offspring-weight-0'), '1.35');
    });

    // Go back to Step 2
    await tap('prev-step-3');
    expect(screen.getByText('Dificultad del parto')).toBeTruthy();

    // Go back to Step 1
    await tap('prev-step-2');
    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();

    // Re-enter
    await pickDam();
    await goToStep3();

    // Calves still intact
    expect(screen.getByText('Crías: 3')).toBeTruthy();
    expect(screen.getByText(/M: 1 · F: 2/)).toBeTruthy();

    // Proceed to Step 4
    await goToStep4();
    expect(screen.getByText(/Dificultad: Asistido/)).toBeTruthy();
    expect(screen.getByText('Crías: 3')).toBeTruthy();

    // Back to Step 3 from Step 4
    await tap('prev-step-4');
    expect(screen.getByText('Crías: 3')).toBeTruthy();
  });

  it('T5.17: confirms and enqueues with pregnancyId and without sire', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    await pickDam();
    await goToStep3();
    await tapAdd('female');
    await goToStep4();

    await tap('confirm-birth');

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    const payload = recordBirth.mock.calls[0][0];
    expect(payload.damId).toBe('dam-1');
    expect(payload.pregnancyId).toBe('preg-1');
    expect(payload.sireAnimalId).toBeUndefined();
    expect(payload.sireStrawId).toBeUndefined();
    expect(payload.difficulty).toBe('Normal');
    expect(payload.offspring).toEqual([{ sex: 'F' }]);
  });

  it('T5.18: handles large litter of 20 calves with accessible counter and buttons', async () => {
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} />);

    await pickDam();
    await goToStep3();

    for (let i = 0; i < 20; i++) {
      await tapAdd(i % 2 === 0 ? 'male' : 'female');
    }

    expect(screen.getByText('Crías: 20')).toBeTruthy();
    expect(screen.getByText(/M: 10 · F: 10/)).toBeTruthy();
    expect(screen.getByTestId('add-male')).toBeTruthy();
    expect(screen.getByTestId('add-female')).toBeTruthy();
    expect(screen.getByTestId('next-step-3')).toBeTruthy();

    await goToStep4();
    expect(screen.getByText('Crías: 20')).toBeTruthy();
    await tap('confirm-birth');

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    expect(recordBirth.mock.calls[0][0].offspring).toHaveLength(20);
  });

  it('cancel clears the wizard and resets to Step 1', async () => {
    const onCancel = jest.fn();
    await render(<BirthScreen service={buildService()} dams={[sampleDam]} onCancel={onCancel} />);

    await pickDam();
    await goToStep3();
    await tapAdd('male');
    await goToStep4();

    await tap('cancel-birth');

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(await screen.findByTestId('dam-dam-1')).toBeTruthy();
    expect(screen.queryByTestId('offspring-count')).toBeNull();
  });
});
