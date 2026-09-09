import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { useTheme } from '../ui/theme';
import { BigButton, Body, Card, Screen, Title } from '../ui/components';
import { RecordStatusBadge, type SyncRecordStatus } from '../ui/recordStates';

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

export interface RecentHubEntry {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  status: SyncRecordStatus;
}

export interface ActivitiesHubProps {
  /** Pending count, surfaced so the operator sees how much is unsent before choosing. */
  pending: number;
  onSelect: (route: ActivityRoute) => void;
  productionOn?: boolean;
  permissions?: string[];
  recentEntries?: RecentHubEntry[];
}

/**
 * Operations hub: direct access to the canonical subjects, prominent tag search,
 * and authorized quick registrations (T7.1–T7.7).
 *
 * Preserves the canonical subject ordering (animal < today < birth < lot) and all testIDs.
 */
export function ActivitiesHub({
  pending,
  onSelect,
  productionOn = false,
  permissions,
  recentEntries,
}: ActivitiesHubProps) {
  const { theme: activeTheme } = useTheme();

  const canVaccinate = !permissions || permissions.includes('livestock.animals.write');
  const canTreat = !permissions || permissions.includes('livestock.animals.write');
  const canEvents = !permissions || permissions.includes('livestock.animals.write');
  const canEdit = !permissions || permissions.includes('livestock.animals.write');
  const canMilk = productionOn && (!permissions || permissions.includes('production.milk.write'));

  return (
    <Screen testID="activities-hub" scrollable>
      <Title>HATO</Title>

      {/* T7.1: Estado de trabajo local y de sincronización */}
      <Card testID="home-work-status">
        <View style={styles.statusBox}>
          <Text
            testID="home-status-indicator"
            style={[styles.statusTitle, { color: activeTheme.color.text, fontSize: activeTheme.font.body }]}
          >
            {pending === 0 ? 'Sincronizado con el servidor' : `● ${pending} registro(s) pendiente(s) de enviar`}
          </Text>
          <Body testID="home-pending" muted>
            {`${pending} registro(s) sin enviar`}
          </Body>
        </View>
      </Card>

      {/* T7.2: Acceso destacado a buscar arete */}
      <BigButton
        testID="home-search-tag"
        label="🔍 Buscar arete en el hato"
        tone="primary"
        hint="Búsqueda rápida por número de arete o identificación interna"
        onPress={() => onSelect('animal-subject')}
      />

      {/* T7.3 / T7.4 / T7.5: Destinos principales en orden canónico */}
      <Card>
        <Body muted>{'Destinos principales'}</Body>
        <BigButton
          testID="subject-animal"
          label="Un animal"
          hint="Ficha, historial y actividades individuales"
          onPress={() => onSelect('animal-subject')}
        />
        <BigButton
          testID="subject-today"
          label="Lo que registré hoy"
          tone="neutral"
          hint="Historial y estado de los registros de la jornada"
          onPress={() => onSelect('today')}
        />
        <BigButton
          testID="subject-birth"
          label="Un parto"
          tone="neutral"
          hint="Registro guiado de parto con madres gestantes"
          onPress={() => onSelect('birth')}
        />
        <BigButton
          testID="subject-lot"
          label="Un lote"
          tone="neutral"
          hint="Pesaje muestral, alimentación y sanidad colectiva"
          onPress={() => onSelect('lot-subject')}
        />
      </Card>

      {/* T7.3 & T7.6: Acciones directas respetando permisos y módulos activos */}
      <Card>
        <Body muted>{'Acciones directas'}</Body>
        {canVaccinate ? (
          <BigButton
            testID="subject-vaccinate"
            label="Vacunar"
            tone="neutral"
            onPress={() => onSelect('vaccinate')}
          />
        ) : null}
        {canTreat ? (
          <BigButton
            testID="subject-treat"
            label="Tratar animal"
            tone="neutral"
            onPress={() => onSelect('treat')}
          />
        ) : null}
        {canEvents ? (
          <BigButton
            testID="subject-events"
            label="Eventos"
            tone="neutral"
            onPress={() => onSelect('events')}
          />
        ) : null}
        {canMilk ? (
          <BigButton
            testID="subject-milking"
            label="Ordeño"
            tone="neutral"
            onPress={() => onSelect('milking')}
          />
        ) : null}
        {canEdit ? (
          <BigButton
            testID="subject-edit"
            label="Editar animal"
            tone="neutral"
            onPress={() => onSelect('editAnimal')}
          />
        ) : null}
        <BigButton
          testID="subject-sync"
          label="Sincronización"
          tone="neutral"
          onPress={() => onSelect('sync')}
        />
      </Card>

      {/* T7.3: Registros recientes propios en este teléfono */}
      {recentEntries && recentEntries.length > 0 ? (
        <Card testID="home-recent-entries">
          <Body muted>Registros recientes en este teléfono</Body>
          {recentEntries.map((entry) => (
            <View key={entry.clientOperationId} style={styles.recentRow}>
              <View style={styles.recentInfo}>
                <Text style={[styles.recentOpText, { color: activeTheme.color.text, fontSize: activeTheme.font.label }]}>
                  {formatOperationName(entry.operationType)}
                </Text>
                <Text style={[styles.recentDateText, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.micro }]}>
                  {entry.occurredAt
                    ? new Date(entry.occurredAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                    : 'Hoy'}
                </Text>
              </View>
              <RecordStatusBadge status={entry.status} />
            </View>
          ))}
        </Card>
      ) : null}
    </Screen>
  );
}

function formatOperationName(type: string): string {
  switch (type) {
    case 'recordIndividualYield':
      return '🥛 Ordeño';
    case 'recordTreatment':
      return '🩺 Tratamiento';
    case 'recordVaccination':
      return '💉 Vacunación';
    case 'recordWeight':
      return '⚖️ Pesaje';
    case 'recordDisposal':
      return '✕ Baja de animal';
    case 'recordGroupMove':
      return '⇄ Cambio de lote';
    case 'recordBirth':
      return '🐣 Parto';
    case 'recordFeedConsumption':
      return '🌾 Consumo alimento';
    case 'recordGroupEvent':
      return '👥 Evento de lote';
    case 'editAnimal':
      return '✏️ Edición de animal';
    default:
      return type;
  }
}

const styles = StyleSheet.create({
  statusBox: {
    gap: 4,
  },
  statusTitle: {
    fontWeight: '800',
  },
  recentRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 6,
    borderBottomWidth: 1,
    borderBottomColor: '#33333333',
  },
  recentInfo: {
    gap: 2,
  },
  recentOpText: {
    fontWeight: '700',
  },
  recentDateText: {
    fontWeight: '500',
  },
});

