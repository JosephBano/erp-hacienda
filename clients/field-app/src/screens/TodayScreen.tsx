import React, { useMemo, useState } from 'react';
import { ScrollView, StyleSheet, TextInput, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, Screen, Title } from '../ui/components';
import type { Outbox } from '../services/outbox';
import type { EventService } from '../services/eventService';

export type TodayEntryStatus = 'pending' | 'synced' | 'rejected' | 'cancelled';

export interface TodayEntry {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  status: TodayEntryStatus;
  resultRef?: string;
}

interface TodayScreenProps {
  entries: TodayEntry[];
  outbox: Outbox;
  events: EventService;
  onChanged: () => void;
}

const TYPE_LABELS: Record<string, string> = {
  recordMilking: 'Ordeño',
  recordAnimalEvent: 'Evento del animal',
  recordBirth: 'Parto',
  recordGroupMilking: 'Ordeño del lote',
  recordTreatment: 'Tratamiento',
  recordGroupEvent: 'Evento del lote',
  recordCorrection: 'Corrección',
  createAnimal: 'Alta de animal',
  moveAnimal: 'Movimiento de lote',
};

const STATUS_LABELS: Record<TodayEntryStatus, string> = {
  pending: 'sin enviar',
  synced: 'en la oficina',
  rejected: 'con problema',
  cancelled: 'cancelado',
};

type CorrectionOutcome = 'cancelled' | 'server-correction' | 'rejected' | 'notFound' | 'busy';

/**
 * "Lo que registré hoy" — the operator's mirror of the outbox, scoped to today.
 *
 * Two labels per row: what it was, and where it stands. The sync-status word on the
 * second line (sin enviar / en la oficina / con problema / cancelado) is what the
 * employee reaches for when they want to know "did my morning reach the office?".
 *
 * The correction flow (3.5a.8) lives on each row:
 *
 *  - Pending    → "Corregir" cancels the outbox entry (Camino A).
 *  - Synced     → "Corregir" enqueues a Correction event with the server's
 *                 resultRef as the originalEventId (Camino B).
 *  - Rejected   → server-side error already shown; the row is read-only.
 *  - Cancelled  → read-only — the cancellation is the correction.
 *
 * The outbox-level pending → cancelled transition is the atomic one
 * (Outbox.cancelPending). If a push lands between the user tapping "Corregir"
 * and the transaction running, the row is no longer pending and the screen
 * falls through to the correction path instead of silently leaving a half-
 * cancelled row.
 */
export function TodayScreen({ entries, outbox, events, onChanged }: TodayScreenProps) {
  const [busyId, setBusyId] = useState<string | null>(null);
  const [reasonFor, setReasonFor] = useState<string | null>(null);
  const [reasonText, setReasonText] = useState('');
  const [outcome, setOutcome] = useState<{ entryId: string; outcome: CorrectionOutcome; message?: string } | null>(null);

  const ordered = useMemo(
    () =>
      [...entries].sort((a, b) => (a.occurredAt < b.occurredAt ? 1 : a.occurredAt > b.occurredAt ? -1 : 0)),
    [entries],
  );

  const cancel = async (entry: TodayEntry) => {
    setBusyId(entry.clientOperationId);
    setOutcome(null);
    try {
      const result = await outbox.cancelPending(entry.clientOperationId);
      if (result === 'cancelled') {
        setOutcome({ entryId: entry.clientOperationId, outcome: 'cancelled' });
        onChanged();
      } else if (result === 'alreadySynced' && entry.resultRef) {
        // The push beat the cancel. Fall through to the correction path so the
        // operator's "I typed it wrong" still becomes a server-side correction
        // instead of being silently dropped.
        setReasonFor(entry.clientOperationId);
        setReasonText('');
      } else if (result === 'alreadyRejected') {
        setOutcome({
          entryId: entry.clientOperationId,
          outcome: 'rejected',
          message: 'El servidor ya rechazó este registro; no se puede corregir desde el teléfono.',
        });
      } else {
        setOutcome({ entryId: entry.clientOperationId, outcome: 'notFound' });
      }
    } finally {
      setBusyId(null);
    }
  };

  const submitCorrection = async (entry: TodayEntry) => {
    if (!entry.resultRef) {
      setOutcome({
        entryId: entry.clientOperationId,
        outcome: 'notFound',
        message: 'No se encontró el identificador del evento original; sincronice primero.',
      });
      setReasonFor(null);
      return;
    }
    if (!reasonText.trim()) {
      return;
    }

    setBusyId(entry.clientOperationId);
    setOutcome(null);
    try {
      await events.recordCorrection({
        originalEventId: entry.resultRef,
        reason: reasonText.trim(),
      });
      setOutcome({ entryId: entry.clientOperationId, outcome: 'server-correction' });
      setReasonFor(null);
      setReasonText('');
      onChanged();
    } finally {
      setBusyId(null);
    }
  };

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
        {ordered.map((entry) => {
          const isCorrection = entry.operationType === 'recordCorrection';
          const canCancel = entry.status === 'pending' && !isCorrection;
          const canCorrect = entry.status === 'synced' && !isCorrection && Boolean(entry.resultRef);
          const isBusy = busyId === entry.clientOperationId;
          const showingReason = reasonFor === entry.clientOperationId;
          const outcomeForEntry = outcome?.entryId === entry.clientOperationId ? outcome : null;

          return (
            <Card key={entry.clientOperationId} testID={`today-row-${entry.clientOperationId}`}>
              <Body testID={`today-row-${entry.clientOperationId}-label`}>
                {TYPE_LABELS[entry.operationType] ?? entry.operationType}
              </Body>
              <Body testID={`today-row-${entry.clientOperationId}-status-${entry.status}`} muted>
                {new Date(entry.occurredAt).toLocaleString()} — {STATUS_LABELS[entry.status]}
              </Body>

              {canCancel ? (
                <BigButton
                  testID={`today-row-${entry.clientOperationId}-correct`}
                  label={isBusy ? 'Procesando…' : 'Corregir (cancelar)'}
                  tone="neutral"
                  busy={isBusy}
                  onPress={() => void cancel(entry)}
                />
              ) : null}

              {canCorrect ? (
                <BigButton
                  testID={`today-row-${entry.clientOperationId}-correct`}
                  label={isBusy ? 'Procesando…' : 'Corregir (enviar corrección)'}
                  tone="neutral"
                  busy={isBusy}
                  onPress={() => {
                    setReasonFor(entry.clientOperationId);
                    setReasonText('');
                    setOutcome(null);
                  }}
                />
              ) : null}

              {showingReason ? (
                <View>
                  <Body>¿Qué se corrigió?</Body>
                  <TextInput
                    testID={`today-row-${entry.clientOperationId}-reason`}
                    value={reasonText}
                    onChangeText={setReasonText}
                    placeholder="Ej.: eran 11, no 10"
                    style={styles.input}
                  />
                  <BigButton
                    testID={`today-row-${entry.clientOperationId}-submit`}
                    label={reasonText.trim() ? 'Enviar corrección' : 'Escriba la razón'}
                    tone="primary"
                    disabled={!reasonText.trim()}
                    onPress={() => void submitCorrection(entry)}
                  />
                  <BigButton
                    testID={`today-row-${entry.clientOperationId}-cancel`}
                    label="Cancelar"
                    tone="neutral"
                    onPress={() => {
                      setReasonFor(null);
                      setReasonText('');
                    }}
                  />
                </View>
              ) : null}

              {outcomeForEntry ? (
                <Notice
                  tone="warning"
                  text={
                    outcomeForEntry.outcome === 'cancelled'
                      ? 'Cancelado. No se envió nada al servidor.'
                      : outcomeForEntry.outcome === 'server-correction'
                        ? 'Corrección en cola. Se enviará en el próximo sync.'
                        : outcomeForEntry.message ?? 'No se pudo corregir.'
                  }
                />
              ) : null}
            </Card>
          );
        })}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
  input: {
    borderColor: theme.color.border,
    borderWidth: 1,
    borderRadius: theme.radius.md,
    padding: theme.space.sm,
    color: theme.color.text,
  },
});
