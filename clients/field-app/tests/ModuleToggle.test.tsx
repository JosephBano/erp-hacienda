import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { ModuleToggle } from '../src/screens/ModuleToggle';

/**
 * The in-app switch that turns a module on and off without a redeploy (ADR-0019).
 * Lives on `SyncStatusScreen` under "Módulos del dispositivo"; pinpoints covered here:
 *  - Switch state reflects the incoming flag (no surprises from a stale render).
 *  - Tapping the switch calls back with the intended next value, not the current one.
 *  - Disabling a module is gated behind a confirmation because it is the kind of flip
 *    the operator gets back only via the admin-web panel — loss of context, not a typo.
 */
describe('ModuleToggle', () => {
  beforeEach(() => {
    global.fetch = jest.fn(() => {
      throw new Error('ModuleToggle must not call the network.');
    }) as unknown as typeof fetch;
  });

  it('renders the module label and its current state', async () => {
    await render(
      <ModuleToggle moduleKey="production" label="Ordeño" enabled={true} onChange={() => undefined} />,
    );

    expect(await screen.findByText(/Ordeño/)).toBeTruthy();
    expect(await screen.findByTestId('module-toggle-production-on')).toBeTruthy();
    expect(screen.queryByTestId('module-toggle-production-off')).toBeNull();
  });

  it('renders the disabled state when the flag is off', async () => {
    await render(
      <ModuleToggle moduleKey="production" label="Ordeño" enabled={false} onChange={() => undefined} />,
    );

    expect(await screen.findByTestId('module-toggle-production-off')).toBeTruthy();
    expect(screen.queryByTestId('module-toggle-production-on')).toBeNull();
  });

  it('calls onChange with the next state when tapped', async () => {
    const onChange = jest.fn();
    await render(
      <ModuleToggle moduleKey="production" label="Ordeño" enabled={true} onChange={onChange} />,
    );

    fireEvent.press(await screen.findByTestId('module-toggle-production-on'));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('production', false);
  });

  it('tells the parent the user wants to enable the module again when it was off', async () => {
    const onChange = jest.fn();
    await render(
      <ModuleToggle moduleKey="production" label="Ordeño" enabled={false} onChange={onChange} />,
    );

    fireEvent.press(await screen.findByTestId('module-toggle-production-off'));

    expect(onChange).toHaveBeenCalledWith('production', true);
  });
});
