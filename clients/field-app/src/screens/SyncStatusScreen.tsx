import React, { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, Notice, Screen, Title } from '../ui/components';
import type { Outbox, OutboxEntry, OutboxStats } from '../services/outbox';
import type { SyncEngine, SyncResult } from '../services/syncEngine';
import type { ModuleKey, ModuleVisibility } from '../services/moduleVisibility';
import { ModuleToggle } from './ModuleToggle';

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
  visibility,
  onModulesChanged,
}: {
  engine: SyncEngine;
  outbox: Outbox;
  visibility: ModuleVisibility;
  onModulesChanged?: () => void;
}) {
  const [stats, setStats] = useState<OutboxStats | null>(null);
  const [rejected, setRejected] = useState<OutboxEntry[]>([]);
  const [result, setResult] = useState<SyncResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [moduleFlags, setModuleFlags] = useState<Record<ModuleKey, boolean>>({
    production: true,
    livestock: true,
    inventory: true,
    breeding: true,
    tasks: true,
    people: true,
  });
  const [pendingConfirm, setPendingConfirm] = useState<{ key: ModuleKey; wasEnabled: boolean } | null>(null);
  const [moduleError, setModuleError] = useState<string | null>(null);
  const [showRedownloadConfirm, setShowRedownloadConfirm] = useState(false);

  const refresh = useCallback(async () => {
    setStats(await outbox.stats());
    setRejected(await outbox.rejected());
    const flags = {
      production: await visibility.canShow('production'),
      livestock: await visibility.canShow('livestock'),
      inventory: await visibility.canShow('inventory'),
      breeding: await visibility.canShow('breeding'),
      tasks: await visibility.canShow('tasks'),
      people: await visibility.canShow('people'),
    };
    setModuleFlags(flags);
  }, [outbox, visibility]);

  useEffect(() => {
    void refresh();
    const unsubscribe = engine.subscribe?.((res) => {
      if (res.pulled > 0 || res.pushed > 0 || res.rejected > 0) {
        void refresh();
      }
    });
    return () => {
      unsubscribe?.();
    };
  }, [engine, refresh]);

  const sync = async () => {
    setBusy(true);
    try {
      setResult(await engine.syncNow());
    } finally {
      await refresh();
      setBusy(false);
    }
  };

  const confirmRedownload = async () => {
    setShowRedownloadConfirm(false);
    setBusy(true);
    try {
      await engine.resetMirror();
      setResult(await engine.syncNow());
    } finally {
      await refresh();
      setBusy(false);
    }
  };

  const onToggleModule = (key: ModuleKey, nextEnabled: boolean) => {
    setModuleError(null);
    if (!nextEnabled) {
      // Disable is the destructive direction: the only way to undo a local disable is
      // an admin-web visit. Pause and ask before committing.
      setPendingConfirm({ key, wasEnabled: moduleFlags[key] });
      return;
    }
    void enableNow(key, true);
  };

  const enableNow = async (key: ModuleKey, enabled: boolean) => {
    await visibility.setEnabled(key, enabled);
    await refresh();
    onModulesChanged?.();
  };

  const confirmDisable = async () => {
    if (!pendingConfirm) return;
    const { key } = pendingConfirm;
    setPendingConfirm(null);
    try {
      await enableNow(key, false);
    } catch (caught) {
      setModuleError((caught as Error).message);
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
      <BigButton
        testID="redownload"
        label="Rehacer descarga"
        tone="neutral"
        busy={busy}
        onPress={() => setShowRedownloadConfirm(true)}
      />

      {showRedownloadConfirm ? (
        <Card>
          <Body>
            Se volverán a descargar los datos del servidor (el hato, lotes y catálogos). Lo que registraste hoy y aún no se ha enviado se conserva en el teléfono. Requiere conexión a internet. ¿Continuar?
          </Body>
          <BigButton
            testID="confirm-redownload"
            label="Sí, rehacer descarga"
            tone="danger"
            busy={busy}
            onPress={confirmRedownload}
          />
          <BigButton
            testID="cancel-redownload"
            label="Cancelar"
            tone="neutral"
            onPress={() => setShowRedownloadConfirm(false)}
          />
        </Card>
      ) : null}

      {result && !result.ok ? (
        <Notice
          tone="warning"
          text={
            result.reason === 'offline'
              ? 'Sin señal. Lo registrado está guardado en el teléfono y se enviará solo cuando haya señal.'
              : result.reason === 'auth'
                ? 'La sesión caducó. Sus registros locales están a salvo. Inicie sesión con contraseña cuando tenga señal para enviarlos.'
                : result.reason === 'pending'
                  ? 'Quedan datos por descargar. Sincronice de nuevo para continuar.'
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

      <Title>Módulos del dispositivo</Title>
      <Body muted>Los cambios se aplican de inmediato al próximo refresh.</Body>
      {moduleError ? <Notice text={moduleError} /> : null}
      <ModuleToggle
        moduleKey="production"
        label="Ordeño"
        enabled={moduleFlags.production}
        onChange={onToggleModule}
      />

      {pendingConfirm ? (
        <Card>
          <Body>
            Apagar Ordeño en el teléfono solo se puede revertir desde el panel admin. ¿Continuar?
          </Body>
          <BigButton testID="confirm-disable-production" label="Sí, apagar" tone="danger" onPress={confirmDisable} />
          <BigButton testID="cancel-disable-production" label="Cancelar" tone="neutral" onPress={() => setPendingConfirm(null)} />
        </Card>
      ) : null}
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
