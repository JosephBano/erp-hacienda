import React, { useMemo } from 'react';
import { ScrollView, StyleSheet } from 'react-native';

import { theme } from '../ui/theme';
import { Body, Card, EmptyState, Screen, Title } from '../ui/components';

export type TodayEntryStatus = 'pending' | 'synced' | 'rejected' | 'cancelled';

export interface TodayEntry {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  status: TodayEntryStatus;
}

interface TodayScreenProps {
  entries: TodayEntry[];
  onSelectEntry: (clientOperationId: string) => void;
}

const TYPE_LABELS: Record<string, string> = {
  recordMilking: 'Ordeño',
  recordAnimalEvent: 'Evento del animal',
  recordBirth: 'Parto',
  recordGroupMilking: 'Ordeño del lote',
  recordTreatment: 'Tratamiento',
  createAnimal: 'Alta de animal',
  moveAnimal: 'Movimiento de lote',
};

const STATUS_LABELS: Record<TodayEntryStatus, string> = {
  pending: 'sin enviar',
  synced: 'en la oficina',
  rejected: 'con problema',
  cancelled: 'cancelado',
};

/**
 * "Lo que registré hoy" — the operator's mirror of the outbox, scoped to today.
 *
 * Two labels per row: what it was, and where it stands. The sync-status word on the
 * second line (sin enviar / en la oficina / con problema / cancelado) is what the
 * employee reaches for when they want to know "did my morning reach the office?".
 *
 * Tapping a row hands off to the parent — the parent owns the navigation to a future
 * 3.5a.8 corrections screen, so this component does not grow that knowledge here.
 */
export function TodayScreen({ entries, onSelectEntry }: TodayScreenProps) {
  const ordered = useMemo(
    () =>
      [...entries].sort((a, b) => (a.occurredAt < b.occurredAt ? 1 : a.occurredAt > b.occurredAt ? -1 : 0)),
    [entries],
  );

  if (ordered.length === 0) {
    return (
      <Screen testID="today-screen">
        <Title>Lo que registré hoy</Title>
        <EmptyState
          testID="today-empty"
          title="Sin registros hoy"
          hint="Lo que se registre desde el teléfono aparecerá aquí."
        />
      </Screen>
    );
  }

  return (
    <Screen testID="today-screen">
      <Title>Lo que registré hoy</Title>
      <ScrollView testID="today-list" contentContainerStyle={styles.list}>
        {ordered.map((entry) => (
          <Card
            key={entry.clientOperationId}
            testID={`today-row-${entry.clientOperationId}`}
            onPress={() => onSelectEntry(entry.clientOperationId)}
          >
            <Body testID={`today-row-${entry.clientOperationId}-label`}>
              {TYPE_LABELS[entry.operationType] ?? entry.operationType}
            </Body>
            <Body testID={`today-row-${entry.clientOperationId}-status-${entry.status}`} muted>
              {new Date(entry.occurredAt).toLocaleString()} — {STATUS_LABELS[entry.status]}
            </Body>
          </Card>
        ))}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
});
