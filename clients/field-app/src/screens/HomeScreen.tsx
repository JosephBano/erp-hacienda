import React from 'react';

import { BigButton, Body, Screen, Title } from '../ui/components';
import type { TabKey } from './navigation';

interface HomeScreenProps {
  /** Display name of the logged-in user, surfaced in the title. Empty string = no name. */
  userName: string;
  /**
   * Whether the Production (milking) module is visible right now. Decoupled from the
   * DB on purpose: HomeScreen is a presentation component, the visibility decision
   * belongs to App-level state that knows what the local `farm_modules` row says.
   */
  productionOn: boolean;
  /** Outbox count to surface in the header — the employee wants to see this first thing. */
  pending: number;
  /** Tab switcher — never called with 'home' from this screen, by definition. */
  onSelectTab: (tab: Exclude<TabKey, 'home'>) => void;
}

/**
 * The home menu: the row of BigButtons the employee can hit in a single tap from any
 * other screen. Module visibility (ADR-0019) hides entries whose flag is off — for
 * the pig pilot, Ordeño — without removing the screen behind them (the data path stays
 * open for outbox milking entries already enqueued).
 *
 * Routed destinations are deliberate: every button is one tap, no submenu, no icon
 * guessing. The cost of an extra layer between a gloved thumb and a record is a layer
 * that can go wrong at 5 AM.
 */
export function HomeScreen({ userName, productionOn, pending, onSelectTab }: HomeScreenProps) {
  return (
    <Screen testID="home-screen">
      <Title>{userName ? `Hola, ${userName}` : 'HATO'}</Title>
      <Body testID="home-pending">{`${pending} registro(s) sin enviar`}</Body>
      {productionOn ? (
        <BigButton testID="go-milking" label="Ordeño" onPress={() => onSelectTab('milking')} />
      ) : null}
      <BigButton testID="go-events" label="Eventos" tone="neutral" onPress={() => onSelectTab('events')} />
      <BigButton testID="go-birth" label="Parto" tone="neutral" onPress={() => onSelectTab('birth')} />
      <BigButton testID="go-edit-animal" label="Editar animal" tone="neutral" onPress={() => onSelectTab('editAnimal')} />
      <BigButton testID="go-sync" label="Sincronización" tone="neutral" onPress={() => onSelectTab('sync')} />
    </Screen>
  );
}
