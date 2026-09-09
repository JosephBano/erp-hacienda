import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { ActivitiesHub } from '../src/screens/ActivitiesHub';

/**
 * 3.5a.9-B: the home becomes an activities hub. Subjects are the first level (the same
 * XOR animal/group that ADR-0015 put into animal_events). The routes are deliberate:
 * an animal activity (treatment, weighing, move) reaches EventsScreen with the animal
 * pre-selected; a birth is BirthScreen; "today" is a brand-new screen that lists the
 * outbox entries the operator recorded on this phone today; a lot activity (3.5a.7)
 * reaches LotSubjectScreen with the lot subject picker.
 *
 * Subject order is the macro-plan question that the client meeting answers
 * (docs/spec/plan-0002-fase-3-5/spec.md sec. 7-C). For now the order is the responsible default of
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
    // The fourth subject ("lote") became a real route in 3.5a.7 tasks 1–5 (pesaje
    // muestral, baja con causa, vacunación/tratamiento de lote, diagnóstico grupal,
    // consumo de alimento), closing the compuerta ADR-0021 left open for the second
    // level of this branch.
    expect(screen.getByTestId('subject-lot')).toBeTruthy();
  });

  it('orders the subjects by the daily-frequency default (animal first, lot last)', async () => {
    await render(<ActivitiesHub pending={0} onSelect={noop} />);

    // Subjects in the Subjects card only — the "Más opciones" card carries a different
    // set of testIDs (subject-events / subject-edit / subject-sync) and is not part
    // of the order this rule is about. Tab order in testIDs: animal < today < birth
    // < lot. The test reads the array of testIDs in DOM order — if the order
    // is wrong, this assertion fails.
    const order = screen
      .getAllByTestId(/^subject-(animal|today|birth|lot)/)
      .map((node) => node.props.testID);
    expect(order).toEqual(['subject-animal', 'subject-today', 'subject-birth', 'subject-lot']);
  });

  it('routes the lot subject to the lot-activity flow', async () => {
    const onSelect = jest.fn();
    await render(<ActivitiesHub pending={0} onSelect={onSelect} />);

    fireEvent.press(await screen.findByTestId('subject-lot'));

    expect(onSelect).toHaveBeenCalledWith('lot-subject');
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

  it('T7.1: indicates when local data is fully up to date with the server', async () => {
    await render(<ActivitiesHub pending={0} onSelect={noop} />);

    expect(await screen.findByText(/Sincronizado con el servidor/i)).toBeTruthy();
  });

  it('T7.2: provides prominent access to tag search', async () => {
    const onSelect = jest.fn();
    await render(<ActivitiesHub pending={0} onSelect={onSelect} />);

    const searchBtn = await screen.findByTestId('home-search-tag');
    expect(searchBtn).toBeTruthy();
    fireEvent.press(searchBtn);
    expect(onSelect).toHaveBeenCalledWith('animal-subject');
  });

  it('T7.3: renders recent records recorded on this phone when present', async () => {
    const recent = [
      {
        clientOperationId: 'op-1',
        operationType: 'recordTreatment',
        occurredAt: '2026-09-08T10:00:00Z',
        status: 'synced' as const,
      },
      {
        clientOperationId: 'op-2',
        operationType: 'recordWeight',
        occurredAt: '2026-09-08T10:15:00Z',
        status: 'pending' as const,
      },
    ];
    await render(<ActivitiesHub pending={1} onSelect={noop} recentEntries={recent} />);

    expect(await screen.findByTestId('home-recent-entries')).toBeTruthy();
    expect(screen.getByText(/Tratamiento/)).toBeTruthy();
    expect(screen.getByText(/Pesaje/)).toBeTruthy();
  });

  it('T7.6: hides disabled production modules and shows them only when active', async () => {
    const { rerender } = await render(
      <ActivitiesHub pending={0} onSelect={noop} productionOn={false} />,
    );

    // Milking is hidden when production module is off
    expect(screen.queryByTestId('subject-milking')).toBeNull();

    await rerender(
      <ActivitiesHub pending={0} onSelect={noop} productionOn={true} />,
    );

    // Milking is available when production is active
    expect(await screen.findByTestId('subject-milking')).toBeTruthy();
  });

  it('T7.6: respects permission gating for direct actions', async () => {
    // Only milking permission, livestock write missing
    await render(
      <ActivitiesHub
        pending={0}
        onSelect={noop}
        productionOn={true}
        permissions={['production.milk.write']}
      />,
    );

    expect(screen.queryByTestId('subject-vaccinate')).toBeNull();
    expect(screen.queryByTestId('subject-treat')).toBeNull();
    expect(screen.queryByTestId('subject-edit')).toBeNull();
    expect(screen.getByTestId('subject-milking')).toBeTruthy();
    expect(screen.getByTestId('subject-sync')).toBeTruthy();
  });
});
