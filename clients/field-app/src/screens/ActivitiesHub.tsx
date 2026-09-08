import React from 'react';

import { BigButton, Body, Card, Screen, Title } from '../ui/components';

/**
 * Identifier of a leaf destination reachable from the hub.
 *
 * Today's routes include the existing screens (milking, events, birth, editAnimal,
 * sync) plus the three flow destinations introduced by 3.5a.9-B and 3.5a.7:
 *  - 'animal-subject': the animal picker that gates per-animal activities.
 *  - 'today': the on-phone accountability view.
 *  - 'lot-subject': the lot picker that gates the six group-subject activities
 *    (ADR-0015) — pesaje muestral, baja con causa, vacunar/tratar el lote,
 *    diagnóstico grupal, consumo de alimento. Landed in 3.5a.7 tasks 1–5, closing
 *    the compuerta ADR-0021 left open for the second level of the "lote" branch.
 */
export type ActivityRoute =
  | 'animal-subject'
  | 'lot-subject'
  | 'birth'
  | 'today'
  | 'events'
  | 'vaccinate'
  | 'treat'
  | 'editAnimal'
  | 'sync'
  | 'milking'
  | 'animals'
  | 'lots'
  | 'activity';

interface ActivitiesHubProps {
  /** Pending count, surfaced so the operator sees how much is unsent before choosing. */
  pending: number;
  onSelect: (route: ActivityRoute) => void;
}

/**
 * Operations hub: direct access to the canonical subjects and quick registrations.
 *
 * Preserves the canonical subject ordering (animal < today < birth < lot) and all testIDs,
 * while removing superfluous parenthetical explanations and connecting cleanly to the 4 sections.
 */
export function ActivitiesHub({ pending, onSelect }: ActivitiesHubProps) {
  return (
    <Screen testID="activities-hub" scrollable>
      <Title>HATO</Title>
      <Body testID="home-pending">{`${pending} registro(s) sin enviar`}</Body>

      <Card>
        <Body muted>{'Destinos principales'}</Body>
        <BigButton
          testID="subject-animal"
          label="Un animal"
          onPress={() => onSelect('animal-subject')}
        />
        <BigButton
          testID="subject-today"
          label="Lo que registré hoy"
          tone="neutral"
          onPress={() => onSelect('today')}
        />
        <BigButton
          testID="subject-birth"
          label="Un parto"
          tone="neutral"
          onPress={() => onSelect('birth')}
        />
        <BigButton
          testID="subject-lot"
          label="Un lote"
          tone="neutral"
          onPress={() => onSelect('lot-subject')}
        />
      </Card>

      <Card>
        <Body muted>{'Acciones directas'}</Body>
        <BigButton
          testID="subject-vaccinate"
          label="Vacunar"
          tone="neutral"
          onPress={() => onSelect('vaccinate')}
        />
        <BigButton
          testID="subject-treat"
          label="Tratar animal"
          tone="neutral"
          onPress={() => onSelect('treat')}
        />
        <BigButton
          testID="subject-events"
          label="Eventos"
          tone="neutral"
          onPress={() => onSelect('events')}
        />
        <BigButton
          testID="subject-edit"
          label="Editar animal"
          tone="neutral"
          onPress={() => onSelect('editAnimal')}
        />
        <BigButton
          testID="subject-sync"
          label="Sincronización"
          tone="neutral"
          onPress={() => onSelect('sync')}
        />
      </Card>
    </Screen>
  );
}
