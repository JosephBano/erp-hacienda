import React, { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, Notice, Screen, Title } from '../ui/components';
import type { Outbox, OutboxEntry, OutboxStats } from '../services/outbox';
import type { SyncEngine, SyncResult } from '../services/syncEngine';

/**
 * Sync status written for the person carrying the phone, not for the developer.
 *
 * The counter that matters is "sin enviar": how much of today's work exists only here. It
 * is stated in plain words and kept on screen, because an employee who cannot tell whether
 * their morning made it to the office will go back to the notebook.
 */
export function SyncStatusScreen({
  engine,
  outbox,
}: {
  engine: SyncEngine;
  outbox: Outbox;
}) {
  const [stats, setStats] = useState<OutboxStats | null>(null);
  const [rejected, setRejected] = useState<OutboxEntry[]>([]);
  const [result, setResult] = useState<SyncResult | null>(null);
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(async () => {
    setStats(await outbox.stats());
    setRejected(await outbox.rejected());
  }, [outbox]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const sync = async () => {
    setBusy(true);
    try {
      setResult(await engine.syncNow());
    } finally {
      await refresh();
      setBusy(false);
    }
  };

  return (
    <Screen testID="sync-status-screen">
      <Title>Sincronización</Title>

      <Card>
        <Text testID="pending-count" style={styles.big}>
          {stats?.pending ?? '—'}
        </Text>
        <Body>registros sin enviar</Body>
        <Body muted>
          {stats ? `${stats.synced} ya en la oficina · ${stats.rejected} con problema` : ''}
        </Body>
      </Card>

      <BigButton testID="sync-now" label="Enviar ahora" busy={busy} onPress={sync} />

      {result && !result.ok ? (
        <Notice
          tone="warning"
          text={
            result.reason === 'offline'
              ? 'Sin señal. Lo registrado está guardado en el teléfono y se enviará solo cuando haya señal.'
              : result.reason === 'auth'
                ? 'La sesión caducó. Inicie sesión con contraseña cuando tenga señal.'
                : 'No se pudo enviar. Nada se perdió: se reintentará automáticamente.'
          }
        />
      ) : null}

      {result?.ok ? <Body muted>{`Enviados ${result.pushed} · recibidos ${result.pulled}`}</Body> : null}

      <Title>Registros con problema</Title>
      {rejected.length === 0 ? (
        <Body muted>Ninguno. Todo lo registrado fue aceptado.</Body>
      ) : (
        <ScrollView testID="problem-list" contentContainerStyle={styles.list}>
          {rejected.map((entry) => (
            <Card key={entry.clientOperationId}>
              <Body>{LABELS[entry.operationType] ?? entry.operationType}</Body>
              <Body muted>{new Date(entry.occurredAt).toLocaleString()}</Body>
              <View style={styles.reason}>
                <Text style={styles.reasonText}>{entry.errorDetails}</Text>
              </View>
            </Card>
          ))}
        </ScrollView>
      )}
    </Screen>
  );
}

const LABELS: Record<string, string> = {
  recordMilking: 'Ordeño',
  recordAnimalEvent: 'Evento del animal',
  createAnimal: 'Alta de animal',
  recordBirth: 'Parto',
  moveAnimal: 'Movimiento de lote',
};

const styles = StyleSheet.create({
  big: {
    color: theme.color.text,
    fontSize: theme.font.display,
    fontWeight: '800',
  },
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
  reason: {
    backgroundColor: theme.color.danger,
    borderRadius: theme.radius.md,
    padding: theme.space.sm,
  },
  reasonText: {
    color: theme.color.text,
    fontSize: theme.font.label,
    fontWeight: '600',
  },
});
