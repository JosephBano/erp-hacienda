import React, { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, Screen, Title } from '../ui/components';
import type { AnimalGroupsApi, AnimalGroupSummary } from '../services/animalGroupsApi';

export type LotActivity =
  | 'feed'
  | 'weighing'
  | 'vaccination'
  | 'treatment'
  | 'diagnosis'
  | 'disposal';

export interface LotForSubject {
  groupId: string;
  label: string;
}

interface LotSubjectScreenProps {
  lots: LotForSubject[];
  selectedGroupId?: string;
  onSelectLot: (groupId: string) => void;
  onClearSelection: () => void;
  onActivity: (groupId: string, activity: LotActivity) => void;
  /**
   * The lot record read (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.7 task 6, already
   * delivered server-side). Optional: passing it in lets the caller own the fetch
   * lifecycle (App.tsx composes services), and its absence never blocks the six
   * activity buttons below — Art. 9 governs registration, not this read.
   */
  animalGroupsApi?: AnimalGroupsApi;
}

/**
 * The "Un lote" branch of the activity hub (3.5a.7). Same two-state shape as
 * `AnimalSubjectScreen`: picker, then — once a lot is chosen — the six group-subject
 * activities ADR-0015 introduced.
 *
 * Activity order follows the frequency-declared assumption documented in
 * `ActivitiesHub.tsx` and `BACKLOG.md`: alimento is the most frequent touch on a lot
 * (docs/spec/plan-0002-fase-3-5/spec.md sec.2.3, "el más frecuente"), so it leads; pesaje muestral is
 * regular but not daily; vacunar/tratar and diagnóstico are occasional; baja is the
 * least frequent of the six. This order is a placeholder pending the client conversation
 * of sec.7-C — not a claim about what the client actually wants.
 */
export function LotSubjectScreen({
  lots,
  selectedGroupId,
  onSelectLot,
  onClearSelection,
  onActivity,
  animalGroupsApi,
}: LotSubjectScreenProps) {
  const [summary, setSummary] = useState<AnimalGroupSummary | null>(null);
  const [summaryError, setSummaryError] = useState<string | null>(null);

  useEffect(() => {
    setSummary(null);
    setSummaryError(null);

    if (!selectedGroupId || !animalGroupsApi) return;

    let cancelled = false;
    void animalGroupsApi
      .getSummary(selectedGroupId)
      .then((result) => {
        if (!cancelled) setSummary(result);
      })
      .catch(() => {
        if (!cancelled) {
          setSummaryError('Ficha no disponible sin conexión.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [selectedGroupId, animalGroupsApi]);

  if (selectedGroupId) {
    const lot = lots.find((l) => l.groupId === selectedGroupId);
    return (
      // Seven buttons at a 64-unit minimum are 448 units before the title and the
      // summary card ever draw. On the short tablet the employees use that runs off
      // the bottom, and a fixed Screen gives no way down (feature-0006 D1).
      <Screen testID="lot-subject-detail" scrollable>
        <Title>{lot?.label ?? selectedGroupId}</Title>

        {summary ? (
          <Card testID="lot-summary-card">
            <Body>{`${summary.liveHeadCount} cabeza(s) viva(s)`}</Body>
            {summary.headsAffectedByDiagnosis > 0 ? (
              <Body muted>{`${summary.headsAffectedByDiagnosis} cabeza(s) con diagnóstico abierto`}</Body>
            ) : null}
          </Card>
        ) : summaryError ? (
          <Notice tone="warning" text={summaryError} />
        ) : null}

        <BigButton
          testID="lot-activity-feed"
          label="Alimento (sacos)"
          onPress={() => onActivity(selectedGroupId, 'feed')}
        />
        <BigButton
          testID="lot-activity-weighing"
          label="Pesaje muestral"
          tone="neutral"
          onPress={() => onActivity(selectedGroupId, 'weighing')}
        />
        <BigButton
          testID="lot-activity-vaccination"
          label="Vacunar el lote"
          tone="neutral"
          onPress={() => onActivity(selectedGroupId, 'vaccination')}
        />
        <BigButton
          testID="lot-activity-treatment"
          label="Tratar el lote"
          tone="neutral"
          onPress={() => onActivity(selectedGroupId, 'treatment')}
        />
        <BigButton
          testID="lot-activity-diagnosis"
          label="Hay uno enfermo"
          tone="neutral"
          onPress={() => onActivity(selectedGroupId, 'diagnosis')}
        />
        <BigButton
          testID="lot-activity-disposal"
          label="Baja con causa"
          tone="neutral"
          onPress={() => onActivity(selectedGroupId, 'disposal')}
        />
        <BigButton testID="back-to-lot-picker" label="Elegir otro lote" tone="neutral" onPress={onClearSelection} />
      </Screen>
    );
  }

  if (lots.length === 0) {
    return (
      <Screen testID="lot-subject-screen">
        <Title>Un lote</Title>
        <EmptyState
          testID="lot-subject-empty"
          title="No hay lotes en el dispositivo"
          hint="Vaya a Inicio → Sincronización para descargar los lotes."
        />
      </Screen>
    );
  }

  return (
    // The screen scrolls, the list does not. A ScrollView with no bounded height
    // inside a Card that does not flex never scrolls — it just overflows the window,
    // which is the reported defect. One vertical gesture per screen (D1).
    <Screen testID="lot-subject-screen" scrollable>
      <Title>Un lote</Title>
      <Card>
        <Body muted>{'Lotes'}</Body>
        <View testID="lot-list" style={styles.list}>
          {lots.map((lot) => (
            <BigButton
              key={lot.groupId}
              testID={`lot-row-${lot.groupId}`}
              label={lot.label}
              tone="neutral"
              onPress={() => onSelectLot(lot.groupId)}
            />
          ))}
        </View>
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
});
