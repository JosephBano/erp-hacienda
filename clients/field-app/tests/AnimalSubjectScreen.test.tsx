import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react-native';

import { AnimalSubjectScreen } from '../src/screens/AnimalSubjectScreen';

/**
 * The "Un animal" subject picker. Loads of responsibility live here:
 *  - Shows the herd pulled down to this phone.
 *  - Filters by a typed query so the operator does not scroll past 80 pigs.
 *  - Surfaces the last few animals this phone acted on, because that is the common case.
 *  - Once an animal is chosen, reveals the activities for that one animal.
 *
 * The activities themselves are reached through the existing screens where possible
 * (treatment, weighing, move go through EventsScreen) — this screen is the entry point,
 * not a fork of EventsScreen.
 */
describe('AnimalSubjectScreen', () => {
  const animals = [
    { animalId: 'a-1', label: 'Pinta 01' },
    { animalId: 'a-2', label: 'Pinta 02' },
    { animalId: 'a-3', label: 'Negra 03' },
    { animalId: 'a-4', label: 'Ciel 04' },
  ];
  const recentIds = ['a-4', 'a-1'];
  const noop = () => undefined;

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('AnimalSubjectScreen must not call the network.');
    }) as unknown as typeof fetch;
  });

  it('shows the empty-herd message when there are no animals on the phone', async () => {
    await render(
      <AnimalSubjectScreen
        animals={[]}
        recentIds={[]}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByTestId('animal-subject-empty')).toBeTruthy();
    expect(screen.queryByTestId('animal-row-a-1')).toBeNull();
  });

  it('shows recent animals first, before the rest of the herd', async () => {
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={recentIds}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    const recentOrder = screen
      .getAllByTestId(/^animal-row-/)
      .map((node) => node.props.testID);

    // Recent a-4 and a-1 lead, then a-2 and a-3 (the non-recent ones).
    expect(recentOrder).toEqual(['animal-row-a-4', 'animal-row-a-1', 'animal-row-a-2', 'animal-row-a-3']);
  });

  it('filters the herd by the typed query', async () => {
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    await act(async () => {
      fireEvent.changeText(await screen.findByTestId('animal-subject-search'), 'pinta');
    });

    // Only the two Pinta rows remain visible (recent list filters out when searching).
    expect(screen.getByTestId('animal-row-a-1')).toBeTruthy();
    expect(screen.getByTestId('animal-row-a-2')).toBeTruthy();
    expect(screen.queryByTestId('animal-row-a-3')).toBeNull();
    expect(screen.queryByTestId('animal-row-a-4')).toBeNull();
  });

  it('routes to the activity list when an animal is tapped', async () => {
    const onSelectAnimal = jest.fn();
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={onSelectAnimal}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    fireEvent.press(await screen.findByTestId('animal-row-a-1'));

    expect(onSelectAnimal).toHaveBeenCalledWith('a-1');
  });

  it('reveals the activities for the selected animal', async () => {
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        selectedAnimalId="a-1"
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByTestId('activity-treatment')).toBeTruthy();
    expect(screen.getByTestId('activity-weight')).toBeTruthy();
    expect(screen.getByTestId('activity-move')).toBeTruthy();
    expect(screen.getByTestId('activity-disposal')).toBeTruthy();
  });

  it('routes the activity tap to the callback with the chosen activity', async () => {
    const onActivity = jest.fn();
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        selectedAnimalId="a-1"
        onSelectAnimal={noop}
        onActivity={onActivity}
        onClearSelection={noop}
      />,
    );

    fireEvent.press(await screen.findByTestId('activity-weight'));

    expect(onActivity).toHaveBeenCalledWith('a-1', 'weight');
  });

  /** 3.5a.3: the disposal stub is gone now that the mortality causes catalog exists. */
  it('routes "Baja con causa" like any other activity, not as a disabled stub', async () => {
    const onActivity = jest.fn();
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        selectedAnimalId="a-1"
        onSelectAnimal={noop}
        onActivity={onActivity}
        onClearSelection={noop}
      />,
    );

    const disposalButton = await screen.findByTestId('activity-disposal');
    fireEvent.press(disposalButton);

    expect(onActivity).toHaveBeenCalledWith('a-1', 'disposal');
  });
});
