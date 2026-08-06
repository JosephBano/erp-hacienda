import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { HomeScreen } from '../src/screens/HomeScreen';

/**
 * The visible part of "Inicio" — the row of BigButtons that gets the employee where they
 * need to be in a single tap. Modulo visibilidad (ADR-0019): the Ordeño button is gated
 * by the production flag; the rest always renders. If the pig pilot is on, Ordeño must
 * not appear, because the link is not just decorative — a stray tap would lead to a screen
 * the operator is being told does not apply.
 */
describe('HomeScreen', () => {
  const noop = () => undefined;

  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('La pantalla de inicio no debe llamar a la red.');
    }) as unknown as typeof fetch;
  });

  it('shows the Ordeño button when the production module is enabled', async () => {
    await render(
      <HomeScreen userName="" productionOn={true} pending={0} onSelectTab={noop} />,
    );

    expect(await screen.findByTestId('go-milking')).toBeTruthy();
  });

  it('hides the Ordeño button when the production module is disabled', async () => {
    await render(
      <HomeScreen userName="" productionOn={false} pending={0} onSelectTab={noop} />,
    );

    expect(screen.queryByTestId('go-milking')).toBeNull();
    // sanity: home was reached
    expect(await screen.findByTestId('home-screen')).toBeTruthy();
  });

  it('keeps the rest of the menu visible regardless of module flags', async () => {
    await render(
      <HomeScreen userName="" productionOn={false} pending={0} onSelectTab={noop} />,
    );

    expect(await screen.findByTestId('go-events')).toBeTruthy();
    expect(await screen.findByTestId('go-birth')).toBeTruthy();
    expect(await screen.findByTestId('go-edit-animal')).toBeTruthy();
    expect(await screen.findByTestId('go-sync')).toBeTruthy();
  });

  it('routes the Ordeño tap when the module is enabled', async () => {
    const onSelectTab = jest.fn();
    await render(
      <HomeScreen userName="" productionOn={true} pending={0} onSelectTab={onSelectTab} />,
    );

    fireEvent.press(await screen.findByTestId('go-milking'));

    expect(onSelectTab).toHaveBeenCalledWith('milking');
  });
});
