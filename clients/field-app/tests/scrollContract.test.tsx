import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import type { Database } from '@nozbe/watermelondb';

import { ActivitiesHub } from '../src/screens/ActivitiesHub';
import { AnimalEditScreen } from '../src/screens/AnimalEditScreen';
import { BirthScreen } from '../src/screens/BirthScreen';
import { EventsScreen } from '../src/screens/EventsScreen';
import { LotEventsScreen } from '../src/screens/LotEventsScreen';
import { TodayScreen } from '../src/screens/TodayScreen';
import type { AnimalEditService } from '../src/services/animalEditService';
import type { BirthService } from '../src/services/birthService';
import type { EventService } from '../src/services/eventService';
import type { FeedConsumptionService } from '../src/services/feedConsumptionService';
import type { HerdMember, PregnantDam } from '../src/services/herdQueries';
import type { Outbox } from '../src/services/outbox';

/**
 * feature-0006 commit 2, T2.5 — the sweep, one test per screen that owns a scroller.
 *
 * Two defects are pinned here, and both are structural rather than visual:
 *
 *  - **Two owners for one drag (D1).** A `Screen scrollable` wrapped around a screen that
 *    already has a bounded inner scroller makes the inner one swallow the gesture. Each
 *    test counts the host scrollers in the tree; the answer is always one.
 *  - **The swallowed first tap.** React Native defaults `keyboardShouldPersistTaps` to
 *    'never', so on any scroller that holds both a text field and a confirm button the
 *    first tap after typing is spent dismissing the keyboard. `Screen scrollable` sets
 *    'handled' (commit 1); the inner scrollers that stayed inner had to be told
 *    separately, and that is the "toco y no pasa nada" the operators reported.
 *
 * RTL renders through the mock host and measures no layout, so what is asserted is the
 * structure that causes the defect, plus the last control being present and answering a
 * press. The on-device verification is `test-e2e.md` (spec criterion 6).
 */

// AnimalEditScreen loads breeds and categories for the picked animal. The catalog is not
// what is under test here, and the real loaders need a live WatermelonDB.
jest.mock('../src/services/herdQueries', () => ({
  loadBreeds: async () => [{ breedId: 'breed-1', label: 'Holstein' }],
  loadCategories: async () => [{ categoryId: 'cat-1', label: 'Vaca' }],
}));

/** Counts the host scrollers in the rendered tree — the D1 "one vertical gesture" check. */
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

/**
 * Types into a field and lets React apply it. Under React 19 + RTL 14 a `changeText`
 * lands on the next tick, so pressing the confirm button in the same tick would read the
 * previous value — which is not what the employee's thumb does. The flush goes through
 * `waitFor` on purpose: a bare `act()` here nests inside RTL's own act boundary, and the
 * "overlapping act() calls" that follows leaves `IS_REACT_ACT_ENVIRONMENT` off for every
 * later test in the file.
 */
const type = async (testID: string, text: string) => {
  fireEvent.changeText(screen.getByTestId(testID), text);
  await waitFor(() => expect(screen.getByTestId(testID)).toBeTruthy());
};

/** The keyboard contract every scroller that holds a text field has to carry. */
const expectKeyboardSafe = (testID: string) => {
  const scroller = screen.getByTestId(testID);
  expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');
  expect(scroller.props.keyboardDismissMode).toBe('on-drag');
};

// Nothing in this file may reach the network: every screen under test is on a
// registration path (Art. 9).
beforeEach(() => {
  global.fetch = jest.fn(() => {
    throw new Error('Ninguna de estas pantallas debe llamar a la red.');
  }) as unknown as typeof fetch;
});

describe('EventsScreen — the weight form scroller', () => {
  const animal = { animalId: 'a-1', label: 'La Pinta' };

  const renderForm = async (recordWeight = jest.fn().mockResolvedValue({})) => {
    await render(
      <EventsScreen
        service={{ recordWeight } as unknown as EventService}
        database={{} as Database}
        animals={[animal]}
        groups={[]}
        initialAnimalId="a-1"
        initialActivity="weight"
      />,
    );
    return recordWeight;
  };

  it('opens one vertical scroller: the form scrolls, the screen does not', async () => {
    await renderForm();

    expect(await screen.findByTestId('events-form')).toBeTruthy();
    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('lets the form grow past the window instead of clamping it', async () => {
    await renderForm();

    const content = flatten((await screen.findByTestId('events-form')).props.contentContainerStyle);

    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
  });

  it('delivers the first tap on "Registrar pesaje" with the keyboard open', async () => {
    const recordWeight = await renderForm();

    expect(await screen.findByTestId('events-form')).toBeTruthy();
    expectKeyboardSafe('events-form');

    // The employee types the weight — the keyboard is up — and taps once.
    await type('weight-input', '420');
    fireEvent.press(screen.getByTestId('confirm-weight'));

    await waitFor(() => expect(recordWeight).toHaveBeenCalledTimes(1));
    expect(recordWeight).toHaveBeenCalledWith({ animalId: 'a-1', weightKg: 420 });

    // Wait for the screen to finish reacting, so no state update lands after teardown.
    expect(await screen.findByText('Pesaje registrado.')).toBeTruthy();
  });
});

describe('AnimalEditScreen — the edit card scroller', () => {
  const animals: HerdMember[] = [
    {
      animalId: 'a-1',
      label: 'La Pinta',
      sex: 'Female',
      isWithheld: false,
      speciesId: 'species-1',
      speciesIsMilkable: true,
    } as HerdMember,
  ];

  const openForm = async (editAnimal = jest.fn().mockResolvedValue({})) => {
    await render(
      <AnimalEditScreen
        database={{} as Database}
        service={{ editAnimal } as unknown as AnimalEditService}
        animals={animals}
      />,
    );

    fireEvent.press(await screen.findByTestId('edit-animal-a-1'));
    await screen.findByTestId('animal-edit-form');
    return editAnimal;
  };

  it('opens one vertical scroller and lets the card grow past the window', async () => {
    await openForm();

    expect(countScrollers(screen.toJSON())).toBe(1);

    const content = flatten(screen.getByTestId('animal-edit-form').props.contentContainerStyle);
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
  });

  it('delivers the first tap on "Guardar cambios" after typing the birth date', async () => {
    const editAnimal = await openForm();

    expectKeyboardSafe('animal-edit-form');

    await type('edit-birthdate', '2026-03-01');
    fireEvent.press(screen.getByTestId('confirm-edit-animal'));

    await waitFor(() => expect(editAnimal).toHaveBeenCalledTimes(1));
    expect(editAnimal).toHaveBeenCalledWith(
      expect.objectContaining({ animalId: 'a-1', birthDate: '2026-03-01' }),
    );

    expect(await screen.findByText(/en cola/)).toBeTruthy();
  });
});

describe('TodayScreen — the day list scroller', () => {
  const entry = {
    clientOperationId: 'op-1',
    operationType: 'recordAnimalEvent',
    occurredAt: '2026-09-07T12:00:00.000Z',
    status: 'synced' as const,
    resultRef: 'event-1',
  };

  it('delivers the first tap on "Enviar corrección" after typing the reason', async () => {
    const recordCorrection = jest.fn().mockResolvedValue({ clientOperationId: 'op-2' });

    await render(
      <TodayScreen
        entries={[entry]}
        outbox={{ cancelPending: jest.fn() } as unknown as Outbox}
        events={{ recordCorrection } as unknown as EventService}
        onChanged={() => undefined}
      />,
    );

    // The reason field and the button that submits it live in the same row of this list,
    // so the list is the scroller that has to persist taps.
    expect(await screen.findByTestId('today-list')).toBeTruthy();
    expect(countScrollers(screen.toJSON())).toBe(1);
    expectKeyboardSafe('today-list');

    fireEvent.press(screen.getByTestId('today-row-op-1-correct'));
    await screen.findByTestId('today-row-op-1-reason');
    await type('today-row-op-1-reason', 'eran 11, no 10');
    fireEvent.press(screen.getByTestId('today-row-op-1-submit'));

    await waitFor(() => expect(recordCorrection).toHaveBeenCalledTimes(1));
    expect(recordCorrection).toHaveBeenCalledWith({
      originalEventId: 'event-1',
      reason: 'eran 11, no 10',
    });

    expect(await screen.findByText(/Corrección en cola/)).toBeTruthy();
  });
});

describe('BirthScreen — the wizard steps', () => {
  const dam: PregnantDam = {
    animalId: 'dam-1',
    label: 'La Pinta',
    pregnancyId: 'preg-1',
    sireLabel: 'Padre: Inseminación artificial',
    expectedBirthDate: '2026-09-01',
  };

  const openStep2 = async () => {
    await render(
      <BirthScreen
        service={{ recordBirth: jest.fn().mockResolvedValue({}) } as unknown as BirthService}
        dams={[dam]}
      />,
    );

    fireEvent.press(await screen.findByTestId('dam-dam-1'));
    await screen.findByTestId('birth-step-2');
  };

  it('gives each step exactly one scroller — the wizard never nests them', async () => {
    await openStep2();

    // BirthScreen keeps its Screen fixed on purpose: every step brings its own scroller,
    // and a scrollable Screen around them would give the drag two owners (D1).
    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('reaches "Continuar a Crías" on the first tap with the date keyboard open', async () => {
    await openStep2();

    expectKeyboardSafe('birth-step-2');

    await type('birth-date-input', '2026-09-07');
    fireEvent.press(screen.getByTestId('next-step-2'));

    expect(await screen.findByTestId('offspring-list')).toBeTruthy();
  });

  it('reaches a calf row control and the step footer with the tag keyboard open', async () => {
    await openStep2();

    fireEvent.press(screen.getByTestId('next-step-2'));
    fireEvent.press(await screen.findByTestId('add-female'));

    expect(countScrollers(screen.toJSON())).toBe(1);
    expectKeyboardSafe('offspring-list');

    // Typing an arete leaves the keyboard up over the list; the row's own buttons are
    // inside the same scroller and used to lose that first tap.
    await screen.findByTestId('offspring-tag-0');
    await type('offspring-tag-0', 'A-77');
    fireEvent.press(screen.getByTestId('toggle-offspring-0'));
    await waitFor(() => expect(screen.getByLabelText('Cambiar a Hembra')).toBeTruthy());
    expect(screen.getByTestId('offspring-count').props.children).toBe('Crías: 1');

    // "Continuar a Resumen" sits in the fixed footer, outside the list, so it stays
    // reachable however long the litter is (a 20-calf birth is the stress fixture).
    fireEvent.press(screen.getByTestId('next-step-3'));
    expect(await screen.findByTestId('confirm-birth')).toBeTruthy();
  });
});

describe('ActivitiesHub — the home screen', () => {
  it('scrolls as one gesture and answers a press on its last entry', async () => {
    const selected: string[] = [];
    await render(<ActivitiesHub pending={0} onSelect={(route) => selected.push(route)} />);

    const hub = await screen.findByTestId('activities-hub');

    // The hub is the screen the employees named: nine entries in two cards, taller than
    // a short tablet. One scroller, and the last entry answers.
    expect(countScrollers(screen.toJSON())).toBe(1);
    expect(flatten(hub.props.contentContainerStyle).flexGrow).toBe(1);
    expect(flatten(hub.props.contentContainerStyle).flex).toBeUndefined();

    fireEvent.press(screen.getByTestId('subject-sync'));
    expect(selected).toEqual(['sync']);
  });
});

describe('LotEventsScreen — the lot form', () => {
  const renderDiagnosis = async (recordGroupEvent = jest.fn().mockResolvedValue({}), onBack = jest.fn()) => {
    await render(
      <LotEventsScreen
        service={{ recordGroupEvent } as unknown as EventService}
        feedService={{} as FeedConsumptionService}
        database={{} as Database}
        lots={[{ groupId: 'lot-1', label: 'Engorde marzo' }]}
        feedItems={[]}
        medications={[]}
        mortalityCauses={[]}
        groupId="lot-1"
        activity="diagnosis"
        onBack={onBack}
      />,
    );

    return { recordGroupEvent, onBack, scroller: await screen.findByTestId('lot-events-screen') };
  };

  it('rides one gesture from the title to "Volver"', async () => {
    const { onBack, scroller } = await renderDiagnosis();

    // Screen scrollable with no inner scroller: the three fields and both buttons share
    // the same drag, and the keyboard contract comes from Screen itself (commit 1).
    expect(countScrollers(screen.toJSON())).toBe(1);
    expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');

    // "Volver" is the screen's last control, below the form inside that same scroller.
    fireEvent.press(screen.getByTestId('lot-events-back'));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('delivers the first tap on "Registrar diagnóstico" with the keyboard open', async () => {
    const { recordGroupEvent } = await renderDiagnosis();

    await type('diagnosis-count-input', '3');
    await type('diagnosis-condition-input', 'Tos');
    fireEvent.press(screen.getByTestId('confirm-diagnosis'));

    await waitFor(() => expect(recordGroupEvent).toHaveBeenCalledTimes(1));
    // Let the whole cycle settle (confirmation + busy) so nothing updates after teardown.
    expect(await screen.findByText('Diagnóstico de lote registrado.')).toBeTruthy();
    await waitFor(() =>
      expect(screen.getByTestId('confirm-diagnosis').props.accessibilityState.disabled).toBe(false),
    );
  });
});
