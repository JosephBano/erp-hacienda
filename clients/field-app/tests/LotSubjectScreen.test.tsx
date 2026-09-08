import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { LotSubjectScreen } from '../src/screens/LotSubjectScreen';
import type { AnimalGroupsApi, AnimalGroupSummary } from '../src/services/animalGroupsApi';

/**
 * The "Un lote" subject picker (3.5a.7). Two states, mirroring `AnimalSubjectScreen`:
 * picker, then — once a lot is chosen — the six group-subject activities.
 *
 * The lot record (task 6, already delivered server-side as
 * `GET /api/v1/animal-groups/{id}/summary`) is read here, not recalculated — see
 * `animalGroupsApi.ts`. It is a display-only read: the six activity buttons below never
 * depend on it having loaded, so this screen must not call the network to register
 * anything (Art. 9 only governs registration, and this screen has none).
 */
describe('LotSubjectScreen', () => {
  const lots = [
    { groupId: 'lot-1', label: 'Engorde marzo' },
    { groupId: 'lot-2', label: 'Engorde abril' },
  ];
  const noop = () => undefined;

  it('shows the empty-lots message when there are no lots on the phone', async () => {
    await render(
      <LotSubjectScreen lots={[]} onSelectLot={noop} onActivity={noop} onClearSelection={noop} />,
    );

    expect(await screen.findByTestId('lot-subject-empty')).toBeTruthy();
  });

  it('lists the lots on the device', async () => {
    await render(
      <LotSubjectScreen lots={lots} onSelectLot={noop} onActivity={noop} onClearSelection={noop} />,
    );

    expect(await screen.findByTestId('lot-row-lot-1')).toBeTruthy();
    expect(screen.getByTestId('lot-row-lot-2')).toBeTruthy();
  });

  it('routes to lot selection when a lot is tapped', async () => {
    const onSelectLot = jest.fn();
    await render(
      <LotSubjectScreen lots={lots} onSelectLot={onSelectLot} onActivity={noop} onClearSelection={noop} />,
    );

    fireEvent.press(await screen.findByTestId('lot-row-lot-1'));

    expect(onSelectLot).toHaveBeenCalledWith('lot-1');
  });

  it('reveals the six activities for the selected lot', async () => {
    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByTestId('lot-activity-feed')).toBeTruthy();
    expect(screen.getByTestId('lot-activity-weighing')).toBeTruthy();
    expect(screen.getByTestId('lot-activity-vaccination')).toBeTruthy();
    expect(screen.getByTestId('lot-activity-treatment')).toBeTruthy();
    expect(screen.getByTestId('lot-activity-diagnosis')).toBeTruthy();
    expect(screen.getByTestId('lot-activity-disposal')).toBeTruthy();
  });

  it('routes an activity tap to the callback with the lot and the chosen activity', async () => {
    const onActivity = jest.fn();
    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={onActivity}
        onClearSelection={noop}
      />,
    );

    fireEvent.press(await screen.findByTestId('lot-activity-feed'));

    expect(onActivity).toHaveBeenCalledWith('lot-1', 'feed');
  });

  it('never touches the network when no animalGroupsApi is supplied', async () => {
    global.fetch = jest.fn(() => {
      throw new Error('LotSubjectScreen must not call fetch directly.');
    }) as unknown as typeof fetch;

    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByTestId('lot-activity-feed')).toBeTruthy();
  });

  /**
   * "La ficha refleja las bajas" (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.7, pruebas): the
   * summary this screen renders is whatever the server's derived `LiveHeadCount`
   * (ADR-0015 sec.4) says, including after a disposal lowered it. This test proves the
   * wiring — the screen displays what the API returns — without recomputing the count
   * client-side, per the task's explicit instruction to consume the endpoint.
   */
  it('shows the live head count from the lot summary, reflecting recorded disposals', async () => {
    const summaryAfterDisposal: AnimalGroupSummary = {
      groupId: 'lot-1',
      liveHeadCount: 39, // started at 42, a disposal of 3 already landed server-side
      headsAffectedByDiagnosis: 0,
      lastDisposalAt: '2026-08-09T12:00:00Z',
    };
    const api: AnimalGroupsApi = {
      getSummary: jest.fn().mockResolvedValue(summaryAfterDisposal),
    };

    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
        animalGroupsApi={api}
      />,
    );

    expect(await screen.findByText('39 cabeza(s) viva(s)')).toBeTruthy();
    expect(api.getSummary).toHaveBeenCalledWith('lot-1');
  });

  it('degrades to a "sin conexión" notice instead of blocking the activity buttons', async () => {
    const api: AnimalGroupsApi = {
      getSummary: jest.fn().mockRejectedValue(new Error('network down')),
    };

    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
        animalGroupsApi={api}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Ficha no disponible sin conexión.')).toBeTruthy();
    });
    // The six activities are still reachable — a failed read never blocks a write.
    expect(screen.getByTestId('lot-activity-feed')).toBeTruthy();
  });

  it('displays tracking mode badges in the list to clearly distinguish headcount and individual lots (T5.1, T5.6)', async () => {
    const mixedLots = [
      { groupId: 'lot-hc', label: 'Engorde lote A', trackingMode: 'Headcount' },
      { groupId: 'lot-ind', label: 'Vacas Lecheras', trackingMode: 'Individual' },
      { groupId: 'lot-none', label: 'Lote Sin Modo' },
    ];

    await render(
      <LotSubjectScreen lots={mixedLots} onSelectLot={noop} onActivity={noop} onClearSelection={noop} />,
    );

    expect(await screen.findByText('Engorde lote A [Por conteo]')).toBeTruthy();
    expect(screen.getByText('Vacas Lecheras [Individual]')).toBeTruthy();
    expect(screen.getByText('Lote Sin Modo')).toBeTruthy();
  });

  it('displays "Modo: Por conteo" in detail view when a headcount lot is selected (D2, T5.1)', async () => {
    const mixedLots = [
      { groupId: 'lot-hc', label: 'Engorde lote A', trackingMode: 'Headcount' },
      { groupId: 'lot-ind', label: 'Vacas Lecheras', trackingMode: 'Individual' },
    ];

    await render(
      <LotSubjectScreen
        lots={mixedLots}
        selectedGroupId="lot-hc"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByText('Modo: Por conteo')).toBeTruthy();
    expect(screen.getByTestId('lot-detail-tracking-mode')).toBeTruthy();
  });

  it('displays "Modo: Individual" in detail view when an individual lot is selected (D2, T5.1)', async () => {
    const mixedLots = [
      { groupId: 'lot-hc', label: 'Engorde lote A', trackingMode: 'Headcount' },
      { groupId: 'lot-ind', label: 'Vacas Lecheras', trackingMode: 'Individual' },
    ];

    await render(
      <LotSubjectScreen
        lots={mixedLots}
        selectedGroupId="lot-ind"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    expect(await screen.findByText('Modo: Individual')).toBeTruthy();
    expect(screen.getByTestId('lot-detail-tracking-mode')).toBeTruthy();
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

describe('LotSubjectScreen reachability', () => {
  const lots = [
    { groupId: 'lot-1', label: 'Engorde marzo' },
    { groupId: 'lot-2', label: 'Engorde abril' },
  ];
  const noop = () => undefined;

  it('scrolls the seven-button detail instead of cutting it off', async () => {
    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    // Seven buttons × 64 units = 448 before the title and the summary card.
    const content = flatten(screen.getByTestId('lot-subject-detail').props.contentContainerStyle);
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('keeps the last control of the detail pressable', async () => {
    const onClearSelection = jest.fn();
    await render(
      <LotSubjectScreen
        lots={lots}
        selectedGroupId="lot-1"
        onSelectLot={noop}
        onActivity={noop}
        onClearSelection={onClearSelection}
      />,
    );

    fireEvent.press(await screen.findByTestId('back-to-lot-picker'));

    expect(onClearSelection).toHaveBeenCalledTimes(1);
  });

  it('gives the lot picker one scroller — the screen, not the list inside the card', async () => {
    await render(
      <LotSubjectScreen lots={lots} onSelectLot={noop} onActivity={noop} onClearSelection={noop} />,
    );

    // `lot-list` used to be a ScrollView with no bounded height inside a Card that
    // does not flex: it never scrolled, it overflowed. It is a plain View now, and
    // the screen carries the single vertical gesture.
    expect(countScrollers(screen.toJSON())).toBe(1);
    expect(screen.getByTestId('lot-list').props.contentContainerStyle).toBeUndefined();

    const content = flatten(screen.getByTestId('lot-subject-screen').props.contentContainerStyle);
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
  });

  it('keeps the last lot of a long list pressable', async () => {
    const onSelectLot = jest.fn();
    const manyLots = Array.from({ length: 20 }, (_, index) => ({
      groupId: `lot-${index}`,
      label: `Lote ${index}`,
    }));

    await render(
      <LotSubjectScreen
        lots={manyLots}
        onSelectLot={onSelectLot}
        onActivity={noop}
        onClearSelection={noop}
      />,
    );

    fireEvent.press(await screen.findByTestId('lot-row-lot-19'));

    expect(onSelectLot).toHaveBeenCalledWith('lot-19');
  });
});
