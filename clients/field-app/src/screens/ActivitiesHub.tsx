import React from 'react';

import { BigButton, Body, Card, Screen, Title } from '../ui/components';

/**
 * Identifier of a leaf destination reachable from the hub.
 *
 * Today's routes include the existing screens (milking, events, birth, editAnimal,
 * sync) plus the two new flow destinations introduced by 3.5a.9-B:
 *  - 'animal-subject': the animal picker that gates per-animal activities.
 *  - 'today': the on-phone accountability view.
 *
 * The 'lote' subject is rendered as a stub today. It is not a real route yet — it
 * becomes one when 3.5a.7 tasks 1–5 (pesaje muestral, baja con causa, vacunación de
 * lote, diagnóstico grupal, consumo de alimento) merge with a TapBudget validated
 * against the operator. The state of the gate is recorded in ADR-0021.
 */
export type ActivityRoute =
  | 'animal-subject'
  | 'birth'
  | 'today'
  | 'events'
  | 'editAnimal'
  | 'sync'
  | 'milking';

interface ActivitiesHubProps {
  /** Pending count, surfaced so the operator sees how much is unsent before choosing. */
  pending: number;
  /**
   * Routes to the chosen subject/screen. The 'lote' subject is rendered as a stub
   * (disabled button) and is not yet a real route, so the hub does not call
   * `onSelect` for it — the notification is the label itself.
   */
  onSelect: (route: ActivityRoute) => void;
}

/**
 * The hub the operator lands on every morning.
 *
 * Subjects — "un animal", "lo que registré hoy", "un parto", "un lote" — are the four
 * things the macro plan 2.3 names as the first navigation level. Each subject maps to
 * a flow that already exists (or, for the lot subject, to 3.5a.7 which has not landed —
 * see ADR-0021).
 *
 * The order of the subjects is the responsible default. PLAN-FASE-3-5-PORCINO sec. 7-C
 * marks the final order as a question only the client can answer; the test pins the
 * current default so a reorder is a deliberate change, not a regression.
 */
export function ActivitiesHub({ pending, onSelect }: ActivitiesHubProps) {
  return (
    <Screen testID="activities-hub">
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
          testID="subject-lot-stub"
          label="Un lote — pendiente 3.5a.7 (pesaje, baja, vacunación, dx, consumo)"
          tone="neutral"
          disabled
        />
      </Card>

      <Card>
        <Body muted>{'Más opciones'}</Body>
        <BigButton
          testID="subject-events"
          label="Eventos (tratamiento, pesaje, movimiento)"
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
