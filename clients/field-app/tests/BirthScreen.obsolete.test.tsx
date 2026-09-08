import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { BirthScreen } from '../src/screens/BirthScreen';
import type { PregnantDam } from '../src/services/herdQueries';
import type { BirthService } from '../src/services/birthService';

const sampleDam1: PregnantDam = {
  animalId: 'dam-1',
  label: 'La Pinta',
  pregnancyId: 'preg-1',
  sireLabel: 'Padre: Inseminación artificial',
  expectedBirthDate: '2026-09-01',
};

const sampleDam2: PregnantDam = {
  animalId: 'dam-2',
  label: 'Carlota',
  pregnancyId: 'preg-2',
  sireLabel: 'Padre: Don Juan',
  expectedBirthDate: '2026-10-08',
};

describe('BirthScreen — obsolete dam handling and draft preservation (T6.1, T6.2, T6.4, T6.7)', () => {
  let recordBirth: jest.Mock;

  const buildService = () => {
    recordBirth = jest.fn().mockResolvedValue({
      clientOperationId: 'op-1',
      damId: 'dam-2',
      birthDate: '2026-08-17',
      offspringCount: 1,
    });
    return { recordBirth } as unknown as BirthService;
  };

  it('informs when dam becomes obsolete in Step 4, disables confirm, and preserves offspring when changing dam', async () => {
    const service = buildService();
    const { rerender } = await render(
      <BirthScreen service={service} dams={[sampleDam1, sampleDam2]} />,
    );

    // Step 1: Pick dam-1
    await act(async () => {
      fireEvent.press(screen.getByTestId('dam-dam-1'));
    });

    // Step 2: Next to Step 3
    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-2'));
    });

    // Step 3: Add offspring
    await act(async () => {
      fireEvent.press(screen.getByTestId('add-female'));
    });
    await act(async () => {
      fireEvent.changeText(screen.getByTestId('offspring-tag-0'), 'BEC-01');
    });

    // Step 3 -> Step 4 (Review)
    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-3'));
    });
    expect(screen.getByTestId('confirm-birth')).toBeTruthy();
    expect(screen.getByText('Crías: 1')).toBeTruthy();

    // Now simulate sync: dam-1 is removed from active pregnant dams
    await rerender(
      <BirthScreen service={service} dams={[sampleDam2]} />,
    );

    // Obsolete notice is displayed
    expect(await screen.findByText(/ya no existe en el sistema/i)).toBeTruthy();

    // Confirm button is disabled
    const confirmButton = screen.getByTestId('confirm-birth');
    expect(confirmButton.props.accessibilityState?.disabled).toBe(true);

    // "Elegir otra madre" action is available
    const changeDamButton = screen.getByTestId('change-dam');
    expect(changeDamButton).toBeTruthy();

    // Tap "Elegir otra madre" -> returns to Step 1
    await act(async () => {
      fireEvent.press(changeDamButton);
    });

    // Now in Step 1, pick dam-2
    expect(await screen.findByTestId('dam-dam-2')).toBeTruthy();
    await act(async () => {
      fireEvent.press(screen.getByTestId('dam-dam-2'));
    });

    // Navigate back to Step 4
    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-2'));
    });
    // Calves still intact (draft preserved!)
    expect(screen.getByText('Crías: 1')).toBeTruthy();

    await act(async () => {
      fireEvent.press(screen.getByTestId('next-step-3'));
    });

    // In Step 4 with dam-2: warning is gone, confirm button is active
    expect(screen.queryByText(/ya no existe en el sistema/i)).toBeNull();
    const updatedConfirm = screen.getByTestId('confirm-birth');
    expect(updatedConfirm.props.accessibilityState?.disabled).toBe(false);

    // Confirm the birth
    await act(async () => {
      fireEvent.press(updatedConfirm);
    });

    await waitFor(() => expect(recordBirth).toHaveBeenCalledTimes(1));
    const payload = recordBirth.mock.calls[0][0];
    expect(payload.damId).toBe('dam-2');
    expect(payload.pregnancyId).toBe('preg-2');
    expect(payload.offspring).toHaveLength(1);
    expect(payload.offspring[0].farmTag).toBe('BEC-01');
  });
});
