import React, { useCallback, useEffect, useState } from 'react';
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
import { useSingleFlight } from '../ui/useSingleFlight';

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
  /**
   * The latch lives on the two press handlers, not inside `submit`.
   *
   * `submit` is reached two ways — straight from `record` when the litres are
   * plausible, and from "Sí, registrar" when they were not — so latching it too
   * would nest a `runOnce` inside the one `record` already holds, and a nested
   * call is dropped by design: the milking would silently never be enqueued.
   * Guarding the two entry points instead covers each intention exactly once,
   * and it closes the window `busy` never covered: the plausibility read runs
   * before the write, so the button has to be latched from the press itself,
   * not from the moment the outbox is touched.
   */
  const { busy, runOnce } = useSingleFlight();
  /**
   * Set when `evaluatePlausibility` returns 'confirm' for the typed value:
   * holds the number itself so the confirm button can submit it directly,
   * without depending on `liters` still holding the same text (ADR-0022 sec.2).
   */
  const [pendingConfirmation, setPendingConfirmation] = useState<number | null>(null);

  const refreshSummary = useCallback(async () => {
    setSummary(await service.dailySummary());
  }, [service]);

  useEffect(() => {
    void refreshSummary();
  }, [refreshSummary]);

  const submit = useCallback(
    async (value: number, isPlausibilityConfirmed: boolean) => {
      if (!selected) return;

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
    /*
     * Two modes, one vertical gesture each (D1). The picker keeps its own bounded
     * `cow-list` scroller and a pinned daily total, which is what the 5 AM screen needs;
     * the selected-animal form has no scroller of its own and grows with the withdrawal
     * notice, the plausibility notice and its two extra buttons, on top of the numeric
     * keyboard — so there the screen itself scrolls.
     */
    <Screen testID="milking-screen" scrollable={selected !== null}>
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

      {/*
        No `flex: 1` in the form mode: inside the scrollable Screen it would clamp this
        box back to the window height and reintroduce the overflow the scroll is there
        to solve. The picker still needs it (see the style's comment).
      */}
      <View style={selected ? undefined : styles.body}>
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
                  onPress={() => void runOnce(() => submit(pendingConfirmation, true))}
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
              <BigButton
                testID="confirm-milking"
                label="Registrar"
                onPress={() => void runOnce(record)}
                busy={busy}
              />
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
   * instead of collapsing to zero height and leaving a black void in the middle. Picker
   * mode only: the form mode renders this wrapper unstyled so the scrollable Screen can
   * grow past the window.
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
