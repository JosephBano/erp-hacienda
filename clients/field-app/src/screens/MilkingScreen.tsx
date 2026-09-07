import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { Database } from '@nozbe/watermelondb';

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
import { evaluatePlausibility } from '../services/plausibilityService';

export interface MilkingCandidate {
  animalId: string;
  label: string;
  isWithheld: boolean;
  withheldUntil?: string;
  /** Whether the animal's species has the milking flag set. Used to filter the picker. */
  speciesIsMilkable: boolean;
  /**
   * Needed to evaluate plausibility (ADR-0022) locally against
   * `plausibility_ranges`. Optional so existing callers/tests that do not
   * care about the check keep working — an undefined speciesId simply never
   * matches a range, which is the fail-open contract anyway.
   */
  speciesId?: string;
  categoryId?: string | null;
  sex?: string;
  disposedAt?: string;
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
  database,
  candidates,
  recordedBy,
  onRecorded,
}: {
  service: MilkingService;
  /**
   * The local WatermelonDB handle, used to evaluate plausibility (ADR-0022)
   * against the mirrored `plausibility_ranges` table. Never used to reach
   * the network — the check is 100% local (Art. 9).
   */
  database: Database;
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
  /**
   * Set when `evaluatePlausibility` returns 'confirm' for the typed value:
   * holds the number itself so the confirm button can submit it directly,
   * without depending on `liters` still holding the same text (ADR-0022 sec.2).
   */
  const [pendingConfirmation, setPendingConfirmation] = useState<number | null>(null);

  const validCandidates = useMemo(
    () =>
      candidates.filter(
        (candidate) =>
          candidate.speciesIsMilkable &&
          (candidate.sex === undefined || candidate.sex.toLowerCase() === 'female') &&
          !candidate.disposedAt,
      ),
    [candidates],
  );

  const refreshSummary = useCallback(async () => {
    setSummary(await service.dailySummary());
  }, [service]);

  useEffect(() => {
    void refreshSummary();
  }, [refreshSummary]);

  const submit = useCallback(
    async (value: number, isPlausibilityConfirmed: boolean) => {
      if (!selected) return;

      setBusy(true);
      setError(null);
      try {
        await service.recordIndividualYield(
          selected.animalId,
          shift,
          value,
          recordedBy,
          undefined,
          isPlausibilityConfirmed,
        );
        setSelected(null);
        setLiters('');
        setPendingConfirmation(null);
        await refreshSummary();
        onRecorded?.();
      } catch (caught) {
        setError((caught as Error).message);
      } finally {
        setBusy(false);
      }
    },
    [onRecorded, recordedBy, refreshSummary, selected, service, shift],
  );

  const record = async () => {
    if (!selected) return;

    setError(null);
    const value = Number(liters.replace(',', '.'));

    // Only values that could plausibly be a real milking are worth checking
    // against the ranges; NaN/negative/zero are the input guardrail's job
    // (`assertVolume` in milkingService.ts) and produce their own message.
    if (Number.isFinite(value) && value > 0 && selected.speciesId) {
      const verdict = await evaluatePlausibility(database, {
        speciesId: selected.speciesId,
        categoryId: selected.categoryId ?? null,
        magnitude: 'milk_liters',
        value,
      });

      if (verdict === 'block') {
        setError(
          `${value} L está fuera de lo posible para este animal. Verifica el dato.`,
        );
        return;
      }
      if (verdict === 'confirm') {
        setPendingConfirmation(value);
        return;
      }
    }

    await submit(value, false);
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

            {pendingConfirmation !== null ? (
              <>
                <Notice
                  tone="warning"
                  text={`${pendingConfirmation} L es mucho más de lo normal para este animal. ¿Es correcto?`}
                />
                <BigButton
                  testID="milking-confirm-plausibility"
                  label="Sí, registrar"
                  onPress={() => void submit(pendingConfirmation, true)}
                  busy={busy}
                />
                <BigButton
                  testID="milking-cancel-plausibility"
                  label="No, revisar"
                  tone="neutral"
                  onPress={() => setPendingConfirmation(null)}
                />
              </>
            ) : (
              <BigButton testID="confirm-milking" label="Registrar" onPress={record} busy={busy} />
            )}

            <BigButton
              testID="cancel-milking"
              label="Cancelar"
              tone="neutral"
              onPress={() => {
                setSelected(null);
                setLiters('');
                setPendingConfirmation(null);
                setError(null);
              }}
            />
          </Card>
        ) : validCandidates.length === 0 ? (
          <EmptyState
            testID="cow-list-empty"
            title="No hay animales ordeñables"
            hint="No hay hembras activas de especies ordeñables en el hato."
          />
        ) : (
          <ScrollView testID="cow-list" contentContainerStyle={styles.list}>
            {validCandidates.map((candidate) => (
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
