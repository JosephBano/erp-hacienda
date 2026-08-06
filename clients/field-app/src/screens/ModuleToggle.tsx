import React from 'react';

import { BigButton } from '../ui/components';
import type { ModuleKey } from '../services/moduleVisibility';

interface ModuleToggleProps {
  moduleKey: ModuleKey;
  label: string;
  enabled: boolean;
  /**
   * Called with the *intended* next state when the operator taps the switch. The parent
   * is responsible for confirmation UX (disabling Production is the kind of flip that
   * needs an explicit "yes" because flipping it back on later requires the admin-web
   * panel, not a button here) and for persisting the change to the local DB.
   */
  onChange: (moduleKey: ModuleKey, nextEnabled: boolean) => void;
}

/**
 * The pill that turns a module on or off from inside the app. Lives on
 * `SyncStatusScreen` under "Módulos del dispositivo" so the operator can flip the switch
 * without redeploying and without waiting for the next pull — that is ADR-0019 #1 in
 * practice: a flag the product owner owns, editable from a surface the operator already
 * reaches for (sync is the screen you visit when something needs fixing).
 *
 * The component is purely presentational: it does not touch the DB, so the parent's
 * write goes through `ModuleVisibility.setEnabled`, which is the same path the eventual
 * pull will exercise. That keeps the contract single-sourced.
 */
export function ModuleToggle({ moduleKey, label, enabled, onChange }: ModuleToggleProps) {
  const testIdOn = `module-toggle-${moduleKey}-on`;
  const testIdOff = `module-toggle-${moduleKey}-off`;

  if (enabled) {
    return (
      <BigButton
        testID={testIdOn}
        label={`${label}: encendido (toca para apagar)`}
        tone="primary"
        onPress={() => onChange(moduleKey, false)}
      />
    );
  }

  return (
    <BigButton
      testID={testIdOff}
      label={`${label}: apagado (toca para encender)`}
      tone="neutral"
      onPress={() => onChange(moduleKey, true)}
    />
  );
}
