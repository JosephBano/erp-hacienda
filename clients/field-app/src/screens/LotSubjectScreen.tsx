import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { useTheme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  Screen,
  TagBadge,
  Title,
} from '../ui/components';
import type { AnimalGroupsApi, AnimalGroupSummary } from '../services/animalGroupsApi';
import type { AnimalForSubject } from '../services/herdQueries';

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
  trackingMode?: string;
  speciesId?: string;
}

export interface TrackingModeInfo {
  badge: string;
  label: string;
  isHeadcount: boolean;
  isIndividual: boolean;
  description: string;
}

export function getTrackingModeInfo(trackingMode?: string): TrackingModeInfo | null {
  if (!trackingMode) return null;
  const normalized = trackingMode.trim().toLowerCase();
  if (normalized === 'headcount') {
    return {
      badge: '[Por conteo]',
      label: 'Modo: Por conteo',
      isHeadcount: true,
      isIndividual: false,
      description: 'Lote por conteo: el inventario y las actividades se gestionan por número de cabezas, sin aretes individuales.',
    };
  }
  if (normalized === 'individual') {
    return {
      badge: '[Individual]',
      label: 'Modo: Individual',
      isHeadcount: false,
      isIndividual: true,
      description: 'Grupo con identificación individual: cada animal conserva su arete e historial propio.',
    };
  }
  return null;
}

/**
 * Formatea fechas ISO provenientes del servidor para presentación en pantalla (YYYY-MM-DD).
 */
export function formatSummaryDate(isoString?: string | null): string {
  if (!isoString) return '';
  return isoString.split('T')[0];
}

export interface LotSubjectScreenProps {
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
  /** Optional animal list to resolve members when trackingMode is Individual (spec 3.1). */
  animals?: AnimalForSubject[];
  /** Optional session permissions (0008). */
  permissions?: string[];
}

/**
 * The "Un lote" branch of the activity hub & group record (feature-0010 Commit 5).
 * Two states:
 *   picker — shows list of groups with tracking mode, clear distinction between headcount and individual.
 *   detail — group card with tracking mode, calculable server summary with update limitations,
 *            canonical activities with 64pt touch targets, and members when known (Individual only).
 *
 * Requirements:
 * - T5.1: Lista de grupos de animales con su modo de seguimiento.
 * - T5.2: Distinguir identificación individual de conteo con claridad.
 * - T5.3: Solo resúmenes calculables, declarando fecha o limitación de actualización (si summary tiene lastDisposalAt, mostrar fecha; si falla red, notice claro sin bloquear).
 * - T5.4: Si un resumen requiere conexión, su indisponibilidad NO bloquea la captura local.
 * - T5.5: NO se fabrican pesos promedio, existencias, dosis ni indicadores que el backend no proporciona.
 * - T5.6: Un grupo Headcount no muestra datos que implicarían identidad individual.
 */
export function LotSubjectScreen({
  lots,
  selectedGroupId,
  onSelectLot,
  onClearSelection,
  onActivity,
  animalGroupsApi,
  animals,
  permissions,
}: LotSubjectScreenProps) {
  const { theme: activeTheme } = useTheme();
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
    const trackingModeInfo = getTrackingModeInfo(lot?.trackingMode);
    const isIndividual = trackingModeInfo?.isIndividual ?? false;
    const isHeadcount = trackingModeInfo?.isHeadcount ?? false;

    // T5.6: Un grupo Headcount NUNCA renderiza miembros individuales ni aretes.
    // Solo los grupos con modo Individual resuelven miembros cuando se conocen (spec 3.1).
    const members =
      isIndividual && animals && lot
        ? animals.filter(
            (a) =>
              (a.groupName === lot.label || (a as { groupId?: string }).groupId === lot.groupId) &&
              !a.disposedAt,
          )
        : [];

    const canWriteLivestock =
      !permissions || permissions.includes('livestock.animals.write') || permissions.includes('*');

    return (
      // Seven buttons at a 64-unit minimum are 448 units before the title and the
      // summary card ever draw. On the short tablet the employees use that runs off
      // the bottom, and a fixed Screen gives no way down (feature-0006 D1).
      <Screen testID="lot-subject-detail" scrollable>
        {/* Cabecera del lote y modo de seguimiento (T5.1, T5.2) */}
        <Card>
          <Text style={[styles.sectionOverline, { color: activeTheme.color.textMuted }]}>
            FICHA DE GRUPO
          </Text>
          <Title>{lot?.label ?? selectedGroupId}</Title>
          {trackingModeInfo ? (
            <Body muted testID="lot-detail-tracking-mode">
              {trackingModeInfo.label}
            </Body>
          ) : null}

          {/* T5.2: Explicación inequívoca del modo de seguimiento */}
          <View
            style={[
              styles.modeExplainer,
              {
                backgroundColor: activeTheme.color.surfaceRaised,
                borderColor: activeTheme.color.border,
              },
            ]}
          >
            <Text style={[styles.modeExplainerText, { color: activeTheme.color.textMuted }]}>
              {isHeadcount
                ? 'Lote por conteo: el inventario y las actividades se gestionan por número de cabezas, sin aretes individuales.'
                : isIndividual
                  ? 'Grupo con identificación individual: cada animal conserva su arete e historial propio.'
                  : 'Modo de seguimiento no especificado para este grupo.'}
            </Text>
          </View>
        </Card>

        {/* T5.3, T5.5: Solo resúmenes calculables respaldados por el backend.
            NO se fabrican pesos promedio, existencias, dosis ni KPIs sintéticos. */}
        {summary ? (
          <Card testID="lot-summary-card">
            <Text style={[styles.sectionOverline, { color: activeTheme.color.textMuted }]}>
              RESUMEN DEL LOTE
            </Text>
            <Body testID="lot-summary-live-headcount">{`${summary.liveHeadCount} cabeza(s) viva(s)`}</Body>
            {summary.headsAffectedByDiagnosis > 0 ? (
              <Body muted testID="lot-summary-diagnosis-heads">
                {`${summary.headsAffectedByDiagnosis} cabeza(s) con diagnóstico abierto`}
              </Body>
            ) : null}
            {summary.lastDisposalAt ? (
              <Body muted testID="lot-summary-last-disposal">
                {`Última baja: ${formatSummaryDate(summary.lastDisposalAt)}`}
              </Body>
            ) : null}
            {summary.lastVaccinationAt ? (
              <Body muted testID="lot-summary-last-vaccination">
                {`Última vacunación: ${formatSummaryDate(summary.lastVaccinationAt)}`}
              </Body>
            ) : null}
            {summary.lastTreatmentAt ? (
              <Body muted testID="lot-summary-last-treatment">
                {`Último tratamiento: ${formatSummaryDate(summary.lastTreatmentAt)}`}
              </Body>
            ) : null}

            {/* T5.3: Declaración expresa de limitación de actualización */}
            <Body muted testID="lot-summary-disclaimer" style={styles.summaryDisclaimer}>
              Ficha calculada en el servidor. Los registros locales pendientes se reflejarán tras sincronizar.
            </Body>
          </Card>
        ) : summaryError ? (
          <View style={styles.noticeWrapper}>
            <Notice tone="warning" text={summaryError} />
            <Body muted style={styles.offlineCaptureNotice}>
              La captura de actividades locales no requiere conexión y sigue disponible.
            </Body>
          </View>
        ) : null}

        {/* T5.6: Miembros conocidos SOLO para grupos con seguimiento Individual.
            Un grupo Headcount NUNCA muestra datos que implicarían identidad individual. */}
        {isIndividual && members.length > 0 ? (
          <Card testID="lot-members-card">
            <Text style={[styles.sectionOverline, { color: activeTheme.color.textMuted }]}>
              MIEMBROS DEL GRUPO
            </Text>
            <Text style={[styles.cardTitle, { color: activeTheme.color.text }]}>
              {`Animales identificados (${members.length})`}
            </Text>
            <View style={styles.membersList}>
              {members.map((member) => (
                <View
                  key={member.animalId}
                  style={styles.memberRow}
                  testID={`lot-member-${member.animalId}`}
                >
                  <TagBadge tag={member.tag} label={member.label} />
                  <Text style={[styles.memberNameText, { color: activeTheme.color.text }]}>
                    {member.name && member.name !== member.tag
                      ? `${member.name} (${member.label})`
                      : member.label}
                  </Text>
                </View>
              ))}
            </View>
          </Card>
        ) : null}

        {/* Acciones canónicas de lote (ADR-0015, T5.4) */}
        {permissions && !canWriteLivestock ? (
          <Notice
            tone="warning"
            text="No tiene permisos para registrar actividades sobre este lote."
          />
        ) : null}

        <BigButton
          testID="lot-activity-feed"
          label="Alimento (sacos)"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'feed')}
        />
        <BigButton
          testID="lot-activity-weighing"
          label="Pesaje muestral"
          tone="neutral"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'weighing')}
        />
        <BigButton
          testID="lot-activity-vaccination"
          label="Vacunar el lote"
          tone="neutral"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'vaccination')}
        />
        <BigButton
          testID="lot-activity-treatment"
          label="Tratar el lote"
          tone="neutral"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'treatment')}
        />
        <BigButton
          testID="lot-activity-diagnosis"
          label="Hay uno enfermo"
          tone="neutral"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'diagnosis')}
        />
        <BigButton
          testID="lot-activity-disposal"
          label="Baja con causa"
          tone="neutral"
          disabled={Boolean(permissions && !canWriteLivestock)}
          onPress={() => onActivity(selectedGroupId, 'disposal')}
        />
        <BigButton
          testID="back-to-lot-picker"
          label="Elegir otro lote"
          tone="neutral"
          onPress={onClearSelection}
        />
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

      {/* Guía visual sobre modos de seguimiento (T5.1, T5.2) */}
      <Card style={styles.guideCard}>
        <Text style={[styles.sectionOverline, { color: activeTheme.color.textMuted }]}>
          MODOS DE SEGUIMIENTO
        </Text>
        <Text style={[styles.guideText, { color: activeTheme.color.textMuted }]}>
          • <Text style={{ fontWeight: '700', color: activeTheme.color.text }}>[Por conteo]</Text>: seguimiento colectivo de cabezas (sin aretes individuales).
        </Text>
        <Text style={[styles.guideText, { color: activeTheme.color.textMuted }]}>
          • <Text style={{ fontWeight: '700', color: activeTheme.color.text }}>[Individual]</Text>: cada animal dispone de arete e historial propio.
        </Text>
      </Card>

      <Card>
        <Body muted>{'Lotes'}</Body>
        <View testID="lot-list" style={styles.list}>
          {lots.map((lot) => {
            const modeInfo = getTrackingModeInfo(lot.trackingMode);
            const buttonLabel = modeInfo ? `${lot.label} ${modeInfo.badge}` : lot.label;
            return (
              <BigButton
                key={lot.groupId}
                testID={`lot-row-${lot.groupId}`}
                label={buttonLabel}
                tone="neutral"
                onPress={() => onSelectLot(lot.groupId)}
              />
            );
          })}
        </View>
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: 8,
    paddingBottom: 16,
  },
  sectionOverline: {
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 0.8,
    marginBottom: 4,
    textTransform: 'uppercase',
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: '700',
    marginBottom: 8,
  },
  modeExplainer: {
    borderRadius: 8,
    borderWidth: 1,
    padding: 10,
    marginTop: 8,
  },
  modeExplainerText: {
    fontSize: 14,
    lineHeight: 19,
  },
  summaryDisclaimer: {
    marginTop: 6,
    fontStyle: 'italic',
    fontSize: 13,
  },
  noticeWrapper: {
    gap: 6,
  },
  offlineCaptureNotice: {
    fontSize: 13,
    paddingHorizontal: 4,
  },
  guideCard: {
    gap: 4,
  },
  guideText: {
    fontSize: 14,
    lineHeight: 20,
  },
  membersList: {
    gap: 8,
    marginTop: 4,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingVertical: 4,
  },
  memberNameText: {
    fontSize: 15,
    fontWeight: '600',
    flex: 1,
  },
});
