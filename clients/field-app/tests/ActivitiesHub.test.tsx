import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { ActivitiesHub } from '../src/screens/ActivitiesHub';

/**
 * 3.5a.9-B: the home becomes an activities hub. Subjects are the first level (the same
 * XOR animal/group that ADR-0015 put into animal_events). The routes are deliberate:
 * an animal activity (treatment, weighing, move) reaches EventsScreen with the animal
 * pre-selected; a birth is BirthScreen; "today" is a brand-new screen that lists the
 * outbox entries the operator recorded on this phone today.
 *
 * Subject order is the macro-plan question that the client meeting answers
 * (PLAN-FASE-3-5-PORCINO sec. 7-C). For now the order is the responsible default of
 * "what the operator reaches for daily, then what they reach for after that". It is
 * pinned as a test so any reorder is a deliberate, reviewable commit.
 */
describe('ActivitiesHub', () => {
  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('ActivitiesHub must not call the network.');
    }) as unknown as typeof fetch;
  });

  const noop = () => undefined;

  it('lists the four subjects the pilot can reach today', async () => {
    await render(<ActivitiesHub pending={0} onSelect={noop} />);

    expect(await screen.findByTestId('subject-animal')).toBeTruthy();
    expect(screen.getByTestId('subject-birth')).toBeTruthy();
    expect(screen.getByTestId('subject-today')).toBeTruthy();
    // The fourth subject ("lote") is shown as a stub pending 3.5a.1 — it has a
    // different testID because its action is not yet a real route.
    expect(screen.getByTestId('subject-lot-stub')).toBeTruthy();
  });

  it('orders the subjects by the daily-frequency default (animal first, lot last)', async () => {
    await render(<ActivitiesHub pending={0} onSelect={noop} />);

    // Subjects in the Subjects card only — the "Más opciones" card carries a different
    // set of testIDs (subject-events / subject-edit / subject-sync) and is not part
    // of the order this rule is about. Tab order in testIDs: animal < today < birth
    // < lot-stub. The test reads the array of testIDs in DOM order — if the order
    // is wrong, this assertion fails.
    const order = screen
      .getAllByTestId(/^subject-(animal|today|birth|lot)/)
      .map((node) => node.props.testID);
    expect(order).toEqual(['subject-animal', 'subject-today', 'subject-birth', 'subject-lot-stub']);
  });

  it('routes the animal subject to the animal-activity flow', async () => {
    const onSelect = jest.fn();
    await render(<ActivitiesHub pending={0} onSelect={onSelect} />);

    fireEvent.press(await screen.findByTestId('subject-animal'));

    expect(onSelect).toHaveBeenCalledWith('animal-subject');
  });

  it('routes the birth subject to the existing birth screen', async () => {
    const onSelect = jest.fn();
    await render(<ActivitiesHub pending={0} onSelect={onSelect} />);

    fireEvent.press(screen.getByTestId('subject-birth'));

    expect(onSelect).toHaveBeenCalledWith('birth');
  });

  it('routes the today subject to the today screen', async () => {
    const onSelect = jest.fn();
    await render(<ActivitiesHub pending={0} onSelect={onSelect} />);

    fireEvent.press(screen.getByTestId('subject-today'));

    expect(onSelect).toHaveBeenCalledWith('today');
  });

  it('shows how many records are waiting to be sent', async () => {
    await render(<ActivitiesHub pending={3} onSelect={noop} />);

    expect(await screen.findByText(/3 registro\(s\) sin enviar/)).toBeTruthy();
  });
});
