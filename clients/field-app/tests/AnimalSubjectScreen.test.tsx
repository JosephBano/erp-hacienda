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

  it('displays notice and lets operator choose another animal when selected animal disappears (T6.4)', async () => {
    const onClearSelection = jest.fn();
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        selectedAnimalId="deleted-animal-id"
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={onClearSelection}
      />,
    );

    expect(await screen.findByText('Animal no disponible')).toBeTruthy();
    expect(screen.getByText(/ya no existe en el sistema/i)).toBeTruthy();
    expect(screen.queryByTestId('activity-weight')).toBeNull();

    fireEvent.press(screen.getByTestId('back-to-animal-picker'));
    expect(onClearSelection).toHaveBeenCalled();
  });
});

/**
 * feature-0006 commit 2 — reachability contract for this screen.
 *
 * RTL renders through the mock host and measures nothing, so none of this proves a
 * pixel is on screen. What it pins is the structure that *causes* unreachable
 * content: a clamped content box, and a second vertical scroller competing with the
 * screen's own for the same drag (D1). The on-device pass is `test-e2e.md`, and that
 * is the one that closes the spec (criterion 6).
 */
const countScrollers = (node: unknown): number => {
  if (!node || typeof node !== 'object') return 0;
  const element = node as { type?: string; children?: unknown[] };
  const self = element.type === 'RCTScrollView' || element.type === 'ScrollView' ? 1 : 0;
  return (element.children ?? []).reduce<number>((acc, child) => acc + countScrollers(child), self);
};

const flatten = (style: unknown): Record<string, unknown> =>
  Array.isArray(style)
    ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
    : ((style ?? {}) as Record<string, unknown>);

describe('AnimalSubjectScreen reachability', () => {
  const animals = [
    { animalId: 'a-1', label: 'Pinta 01' },
    { animalId: 'a-2', label: 'Pinta 02' },
    { animalId: 'a-3', label: 'Negra 03' },
    { animalId: 'a-4', label: 'Ciel 04' },
  ];
  const noop = () => undefined;

  it('scrolls the activity detail instead of cutting it off', async () => {
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

    const content = flatten(screen.getByTestId('animal-subject-detail').props.contentContainerStyle);
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('keeps the last control of the detail pressable', async () => {
    const onClearSelection = jest.fn();
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        selectedAnimalId="a-1"
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={onClearSelection}
      />,
    );

    fireEvent.press(await screen.findByTestId('back-to-animal-picker'));

    expect(onClearSelection).toHaveBeenCalledTimes(1);
  });

  it('gives the animal picker one scroller — the screen, not the list inside the card', async () => {
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={['a-4']}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    // Both cards are inside the screen's scroller now: "Recientes" had no scroller of
    // its own, and `animal-list` had one that could not scroll.
    expect(countScrollers(screen.toJSON())).toBe(1);
    expect(screen.getByTestId('animal-list').props.contentContainerStyle).toBeUndefined();

    const content = flatten(screen.getByTestId('animal-subject-screen').props.contentContainerStyle);
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
  });

  it('lets the first tap on a result land while the search keyboard is open', async () => {
    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    const scroller = screen.getByTestId('animal-subject-screen');

    // The old inner ScrollView ran on React Native's default 'never', which spends
    // the first tap after typing on dismissing the keyboard: the employee taps an
    // animal, nothing happens, they tap again.
    expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');
    // Dragging the list away from the search field puts the keyboard down and
    // registers nothing (D2).
    expect(scroller.props.keyboardDismissMode).toBe('on-drag');
  });

  it('keeps the last animal of a long herd pressable', async () => {
    const onSelectAnimal = jest.fn();
    const herd = Array.from({ length: 30 }, (_, index) => ({
      animalId: `a-${index}`,
      label: `Vaca ${index}`,
    }));

    await render(
      <AnimalSubjectScreen
        animals={herd}
        recentIds={[]}
        onSelectAnimal={onSelectAnimal}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    fireEvent.press(await screen.findByTestId('animal-row-a-29'));

    expect(onSelectAnimal).toHaveBeenCalledWith('a-29');
  });
});

describe('AnimalSubjectScreen feature-0007 Commit 1 and 2', () => {
  const noop = () => undefined;

  it('displays prominent tag, sex, and group on animal search results (D3, T1.3)', async () => {
    const animals = [
      {
        animalId: 'cow-1',
        label: 'Margarita (CRIA-01)',
        name: 'Margarita',
        tag: 'CRIA-01',
        sex: 'Female',
        groupName: 'Lote Lechero',
        hasPendingTag: false,
      },
    ];

    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByText('Arete: CRIA-01')).toBeTruthy();
    expect(screen.getByText('Hembra')).toBeTruthy();
    expect(screen.getByText('Grupo: Lote Lechero')).toBeTruthy();
  });

  it('displays "Sin arete" badge clearly for untagged animals and allows finding by ID snippet (D5, T1.7)', async () => {
    const animals = [
      {
        animalId: '00000000-0000-0000-0000-000000654321',
        label: 'Sin arete · 654321',
        sex: 'Male',
        hasPendingTag: true,
      },
    ];

    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByTestId('pending-tag-badge-00000000-0000-0000-0000-000000654321')).toBeTruthy();
    expect(screen.getByText('Sin arete')).toBeTruthy();
    expect(screen.getByText('Macho')).toBeTruthy();

    // Search by 6-character snippet
    await act(async () => {
      fireEvent.changeText(await screen.findByTestId('animal-subject-search'), '654321');
    });

    expect(screen.getByTestId('animal-row-00000000-0000-0000-0000-000000654321')).toBeTruthy();
  });

  it('distinguishes exact and partial matches in UI (T1.6)', async () => {
    const animals = [
      { animalId: 'a-10', label: 'COW-10', tag: 'COW-10', hasPendingTag: false },
      { animalId: 'a-100', label: 'COW-100', tag: 'COW-100', hasPendingTag: false },
    ];

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
      fireEvent.changeText(await screen.findByTestId('animal-subject-search'), 'COW-10');
    });

    expect(await screen.findByText(/Coincidencias exactas \(1\)/i)).toBeTruthy();
    expect(screen.getByText(/Coincidencias parciales \(1\)/i)).toBeTruthy();
    expect(screen.getByText('Coincidencia exacta')).toBeTruthy();
    expect(screen.getByText('Coincidencia parcial')).toBeTruthy();
  });

  it('surfaces ambiguous matches when multiple animals share the same tag without auto-selecting or merging (T2.1, T2.2, T2.3, T2.4)', async () => {
    const onSelectAnimal = jest.fn();
    const animals = [
      {
        animalId: 'dup-1',
        label: 'TAG-DUPLICATE (Norte)',
        tag: 'TAG-DUPLICATE',
        sex: 'Female',
        groupName: 'Lote Norte',
        hasPendingTag: false,
      },
      {
        animalId: 'dup-2',
        label: 'TAG-DUPLICATE (Sur)',
        tag: 'TAG-DUPLICATE',
        sex: 'Male',
        groupName: 'Lote Sur',
        hasPendingTag: false,
      },
    ];

    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={onSelectAnimal}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    await act(async () => {
      fireEvent.changeText(await screen.findByTestId('animal-subject-search'), 'TAG-DUPLICATE');
    });

    // 1. Both animals rendered separately (never merged)
    expect(await screen.findByTestId('animal-row-dup-1')).toBeTruthy();
    expect(screen.getByTestId('animal-row-dup-2')).toBeTruthy();

    // 2. Ambiguity warning presented
    expect(screen.getByText(/Múltiples animales coinciden exactamente/i)).toBeTruthy();

    // 3. Data presented is sufficient to distinguish: sex and group
    expect(screen.getByText('Hembra')).toBeTruthy();
    expect(screen.getByText('Grupo: Lote Norte')).toBeTruthy();
    expect(screen.getByText('Macho')).toBeTruthy();
    expect(screen.getByText('Grupo: Lote Sur')).toBeTruthy();

    // 4. Never auto-selects silently
    expect(onSelectAnimal).not.toHaveBeenCalled();

    // 5. Conscious user selection required
    fireEvent.press(screen.getByTestId('animal-row-dup-1'));
    expect(onSelectAnimal).toHaveBeenCalledWith('dup-1');
    expect(onSelectAnimal).not.toHaveBeenCalledWith('dup-2');
  });

  it('displays historical tag badge and notice prominently when matched via previous tag (T3.5, D3)', async () => {
    const onSelectAnimal = jest.fn();
    const animals = [
      {
        animalId: 'hist-animal-1',
        label: 'TAG-CURR',
        tag: 'TAG-CURR',
        sex: 'Female',
        activeIdentifiers: [{ type: 'FarmTag', value: 'TAG-CURR' }],
        historicalIdentifiers: [{ type: 'FarmTag', value: 'TAG-PREV-88' }],
        hasPendingTag: false,
      },
    ];

    await render(
      <AnimalSubjectScreen
        animals={animals}
        recentIds={[]}
        onSelectAnimal={onSelectAnimal}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    await act(async () => {
      fireEvent.changeText(await screen.findByTestId('animal-subject-search'), 'TAG-PREV-88');
    });

    // 1. Animal row is found
    expect(await screen.findByTestId('animal-row-hist-animal-1')).toBeTruthy();

    // 2. Historical tag badge is displayed prominently
    expect(screen.getByTestId('historical-tag-badge-hist-animal-1')).toBeTruthy();
    expect(screen.getByText('Arete anterior: TAG-PREV-88')).toBeTruthy();

    // 3. Current active tag is still displayed as current, never presented as TAG-PREV-88 being active
    expect(screen.getByText('Arete: TAG-CURR')).toBeTruthy();
  });
});

