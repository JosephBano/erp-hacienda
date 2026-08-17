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
  | 'milking';

interface ActivitiesHubProps {
  /** Pending count, surfaced so the operator sees how much is unsent before choosing. */
  pending: number;
  onSelect: (route: ActivityRoute) => void;
}

/**
 * The hub the operator lands on every morning.
 *
 * Subjects — "un animal", "lo que registré hoy", "un parto", "un lote" — are the four
 * things the macro plan 2.3 names as the first navigation level. Each subject maps to a
 * flow that already exists. "Un lote" landed in 3.5a.7: its `TapBudget` is validated by
 * `LotEventsScreen.tapBudget.test.tsx` (ADR-0021 condition 2), so the compuerta ADR-0021
 * left open for the second level of this branch closes with this rama.
 *
 * The order of the subjects is the responsible default. docs/spec/plan-0002-fase-3-5/spec.md sec. 7-C
 * marks the final order as a question only the client can answer; the test pins the
 * current default so a reorder is a deliberate change, not a regression.
 */
export function ActivitiesHub({ pending, onSelect }: ActivitiesHubProps) {
  return (
    <Screen testID="activities-hub" scrollable>
      <Title>HATO</Title>
      <Body testID="home-pending">{`${pending} registro(s) sin enviar`}</Body>

      <Card>
        <Body muted>{'Sujetos'}</Body>
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
          label="Un lote (alimento, pesaje, vacuna, dx, baja)"
          tone="neutral"
          onPress={() => onSelect('lot-subject')}
        />
      </Card>

      <Card>
        <Body muted>{'Más opciones'}</Body>
        <BigButton
          testID="subject-vaccinate"
          label="Vacunar"
          tone="neutral"
          onPress={() => onSelect('vaccinate')}
        />
        <BigButton
          testID="subject-treat"
          label="Tratar animal enfermo"
          tone="neutral"
          onPress={() => onSelect('treat')}
        />
        <BigButton
          testID="subject-events"
          label="Eventos (pesaje, movimiento, baja)"
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
