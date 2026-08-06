import React, { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { theme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  NumberField,
  Screen,
  Title,
} from '../ui/components';
import type { DailySummary, MilkingShift, MilkingService } from '../services/milkingService';

export interface MilkingCandidate {
  animalId: string;
  label: string;
  isWithheld: boolean;
  withheldUntil?: string;
  /** Whether the animal's species has the milking flag set. Used to filter the picker. */
  speciesIsMilkable: boolean;
}

/**
 * The 5 AM screen.
 *
 * Three taps to a record and no more: pick the cow, type the litres on the pad already on
 * screen, confirm. The shift is preselected from the clock because at that hour it is
 * always the morning one, and a cow under an Art. 19 withdrawal is marked *before* the
 * employee reaches for her — refusing the entry after the fact would mean they milked into
 * the wrong bucket.
 */
export function MilkingScreen({
  service,
  candidates,
  recordedBy,
  onRecorded,
}: {
  service: MilkingService;
  candidates: MilkingCandidate[];
  /** Identity to stamp on the outbox payload; the server requires it non-empty. */
  recordedBy: string;
  onRecorded?: () => void;
}) {
  const [selected, setSelected] = useState<MilkingCandidate | null>(null);
  const [liters, setLiters] = useState('');
  const [shift, setShift] = useState<MilkingShift>(currentShift());
  const [summary, setSummary] = useState<DailySummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const refreshSummary = useCallback(async () => {
    setSummary(await service.dailySummary());
  }, [service]);

  useEffect(() => {
    void refreshSummary();
  }, [refreshSummary]);

  const record = async () => {
    if (!selected) return;

    const value = Number(liters.replace(',', '.'));
    setBusy(true);
    setError(null);

    try {
      await service.recordIndividualYield(selected.animalId, shift, value, recordedBy);
      setSelected(null);
      setLiters('');
      await refreshSummary();
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="milking-screen">
      <Title>Ordeño</Title>

      <View style={styles.shifts}>
        {(['Morning', 'Afternoon', 'Evening'] as MilkingShift[]).map((option) => (
          <View key={option} style={styles.shiftItem}>
            <BigButton
              testID={`shift-${option}`}
              label={SHIFT_LABEL[option]}
              tone={shift === option ? 'primary' : 'neutral'}
              onPress={() => setShift(option)}
            />
          </View>
        ))}
      </View>

      {error ? <Notice text={error} /> : null}

      <View style={styles.body}>
        {selected ? (
          <Card>
            <Body>{selected.label}</Body>
            {selected.isWithheld ? (
              <Notice
                tone="warning"
                text={`Leche NO vendible: retiro activo hasta ${selected.withheldUntil}.`}
              />
            ) : null}
            <NumberField
              label="Litros"
              testID="liters-input"
              value={liters}
              onChangeText={setLiters}
              placeholder="0.0"
            />
            <BigButton testID="confirm-milking" label="Registrar" onPress={record} busy={busy} />
            <BigButton
              testID="cancel-milking"
              label="Cancelar"
              tone="neutral"
              onPress={() => {
                setSelected(null);
                setError(null);
              }}
            />
          </Card>
        ) : candidates.length === 0 ? (
          <EmptyState
            testID="cow-list-empty"
            title="No hay animales ordeñables"
            hint="El hato del dispositivo no tiene especies marcadas como ordeñables. Vaya a Inicio → Sincronización para traer las especies actualizadas, o pida al administrador que active la opción 'ordeñable' en la especie desde el panel web."
          />
        ) : (
          <ScrollView testID="cow-list" contentContainerStyle={styles.list}>
            {candidates.map((candidate) => (
              <BigButton
                key={candidate.animalId}
                testID={`cow-${candidate.animalId}`}
                label={candidate.isWithheld ? `${candidate.label}  ⚠ RETIRO` : candidate.label}
                tone={candidate.isWithheld ? 'danger' : 'neutral'}
                disabled={!candidate.speciesIsMilkable}
                hint={
                  candidate.isWithheld
                    ? `Retiro activo hasta ${candidate.withheldUntil}. La leche no es vendible.`
                    : !candidate.speciesIsMilkable
                      ? 'Esta especie no está habilitada para ordeño.'
                      : undefined
                }
                onPress={() => {
                  setSelected(candidate);
                  setError(null);
                }}
              />
            ))}
          </ScrollView>
        )}
      </View>

      <Card>
        <Text testID="daily-total" style={styles.total}>
          {summary ? `${summary.totalLiters} L` : '—'}
        </Text>
        <Body muted>
          {summary ? `${summary.recordsCount} registro(s) hoy` : 'Cargando resumen…'}
        </Body>
      </Card>
    </Screen>
  );
}

const SHIFT_LABEL: Record<MilkingShift, string> = {
  Morning: 'Mañana',
  Afternoon: 'Tarde',
  Evening: 'Noche',
};

/** Preselects the shift so the common case costs zero taps. */
function currentShift(): MilkingShift {
  const hour = new Date().getHours();
  if (hour < 11) return 'Morning';
  if (hour < 17) return 'Afternoon';
  return 'Evening';
}

const styles = StyleSheet.create({
  shifts: {
    flexDirection: 'row',
    gap: theme.space.sm,
  },
  shiftItem: {
    flex: 1,
  },
  /**
   * Fills the remaining vertical space between the shift selector and the daily-total
   * card, so the picker (or its empty state) actually claims the room the layout offers
   * instead of collapsing to zero height and leaving a black void in the middle.
   */
  body: {
    flex: 1,
  },
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
  total: {
    color: theme.color.text,
    fontSize: theme.font.display,
    fontWeight: '800',
  },
});
