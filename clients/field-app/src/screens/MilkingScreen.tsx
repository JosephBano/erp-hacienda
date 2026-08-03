import React, { useCallback, useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, Notice, NumberField, Screen, Title } from '../ui/components';
import type { DailySummary, MilkingShift, MilkingService } from '../services/milkingService';

export interface MilkingCandidate {
  animalId: string;
  label: string;
  isWithheld: boolean;
  withheldUntil?: string;
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
  onRecorded,
}: {
  service: MilkingService;
  candidates: MilkingCandidate[];
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
      await service.recordIndividualYield(selected.animalId, shift, value);
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
      ) : (
        <ScrollView testID="cow-list" contentContainerStyle={styles.list}>
          {candidates.map((candidate) => (
            <BigButton
              key={candidate.animalId}
              testID={`cow-${candidate.animalId}`}
              label={candidate.isWithheld ? `${candidate.label}  ⚠ RETIRO` : candidate.label}
              tone={candidate.isWithheld ? 'danger' : 'neutral'}
              hint={
                candidate.isWithheld
                  ? `Retiro activo hasta ${candidate.withheldUntil}. La leche no es vendible.`
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
