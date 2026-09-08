import React, { useEffect, useState } from 'react';
import { Database } from '@nozbe/watermelondb';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, NumberField, Screen, TextField, Title } from '../ui/components';
import type { EventService } from '../services/eventService';
import type { FeedConsumptionService } from '../services/feedConsumptionService';
import { evaluatePlausibility } from '../services/plausibilityService';
import { useSingleFlight } from '../ui/useSingleFlight';
import type { LotActivity } from './LotSubjectScreen';

export interface LotOption {
  groupId: string;
  label: string;
  speciesId?: string;
}

export interface FeedItemOption {
  itemId: string;
  name: string;
  unit: string;
}

export interface MedicationOption {
  itemId: string;
  name: string;
}

export interface MortalityCauseOption {
  causeId: string;
  name: string;
}

/**
 * The six activities of the "Un lote" branch (3.5a.7 tasks 1–5, plus the vaccination /
 * treatment split — ADR-0015 treats them as distinct event types, so this screen offers
 * two buttons instead of one that hides a picker behind it).
 *
 * Every write here is offline-first (Art. 9): `service.recordGroupEvent` and
 * `feedService.recordFeedConsumption` enqueue to the local outbox and return
 * immediately — nothing in this screen awaits the network. The only network call in the
 * whole "Un lote" branch is the best-effort summary read on `LotSubjectScreen`, which
 * this screen never touches.
 *
 * The lot (and the activity) arrive pre-selected from `LotSubjectScreen`, mirroring how
 * `EventsScreen` receives `initialAnimalId`/`initialActivity` from `AnimalSubjectScreen`
 * — this screen never shows its own lot picker.
 */
export function LotEventsScreen({
  service,
  feedService,
  database,
  lots,
  feedItems,
  medications,
  mortalityCauses,
  groupId,
  activity,
  onRecorded,
  onBack,
}: {
  service: EventService;
  feedService: FeedConsumptionService;
  /** WatermelonDB handle, used only to evaluate plausibility (ADR-0022) — never for network. */
  database: Database;
  lots: LotOption[];
  feedItems: FeedItemOption[];
  medications: MedicationOption[];
  mortalityCauses: MortalityCauseOption[];
  groupId: string;
  activity: LotActivity;
  onRecorded?: () => void;
  onBack: () => void;
}) {
  const lot = lots.find((l) => l.groupId === groupId);

  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);
  const { busy, runOnce } = useSingleFlight();

  // Weighing
  const [weightsText, setWeightsText] = useState('');
  const [weighingPendingConfirmation, setWeighingPendingConfirmation] = useState<{
    sampleCount: number;
    avgKg: number;
    minKg: number;
    maxKg: number;
    weights: number[];
  } | null>(null);

  // Disposal
  const [cause, setCause] = useState<MortalityCauseOption | null>(null);
  const [disposalCount, setDisposalCount] = useState('');

  // Vaccination / treatment
  const [medication, setMedication] = useState<MedicationOption | null>(null);
  const [dose, setDose] = useState('');
  const [headCount, setHeadCount] = useState('');

  // Diagnosis
  const [affectedCount, setAffectedCount] = useState('');
  const [condition, setCondition] = useState('');
  const [notes, setNotes] = useState('');

  // Feed
  const [feedItem, setFeedItem] = useState<FeedItemOption | null>(null);
  const [feedQuantity, setFeedQuantity] = useState('');
  const [feedUnit, setFeedUnit] = useState('');

  // A lot with exactly one feed item is the common case at the pilot: skip the picker
  // tap entirely (same "pre-selection costs zero taps" idea as MilkingScreen's shift).
  useEffect(() => {
    if (activity === 'feed' && feedItems.length === 1) {
      setFeedItem(feedItems[0]);
    }
  }, [activity, feedItems]);

  /**
   * The shared body of every write on this screen: enqueue, confirm, go back.
   *
   * The single-flight latch is *not* taken here but at each press handler, one
   * level up. `recordWeighing` awaits the plausibility check before it ever
   * reaches this function, and that await is the window a double tap lands in
   * (D2). Latching in both places would be worse than in neither: the outer
   * `runOnce` would refuse its own inner one and the write would vanish.
   */
  const run = async (action: () => Promise<unknown>, done: string) => {
    setError(null);
    try {
      await action();
      setConfirmation(done);
      onRecorded?.();
      onBack();
    } catch (caught) {
      setError((caught as Error).message);
    }
  };

  const submitWeighing = async (weights: number[], isPlausibilityConfirmed: boolean) => {
    const sampleCount = weights.length;
    const avgKg = round2(weights.reduce((sum, w) => sum + w, 0) / sampleCount);
    const minKg = Math.min(...weights);
    const maxKg = Math.max(...weights);

    await run(
      () =>
        service.recordGroupEvent({
          groupId,
          eventType: 'Weighing',
          payload: { sampleCount, avgKg, minKg, maxKg, weights, isPlausibilityConfirmed },
        }),
      'Pesaje muestral registrado.',
    );
  };

  const recordWeighing = async () => {
    setError(null);
    const weights = parseWeights(weightsText);

    if (weights.length === 0) {
      setError('Ingrese al menos un peso.');
      return;
    }
    if (weights.some((w) => w <= 0)) {
      setError('Los pesos deben ser mayores que cero.');
      return;
    }

    const avgKg = round2(weights.reduce((sum, w) => sum + w, 0) / weights.length);

    if (lot?.speciesId) {
      const verdict = await evaluatePlausibility(database, {
        speciesId: lot.speciesId,
        categoryId: null,
        magnitude: 'weight_kg',
        value: avgKg,
      });

      if (verdict === 'block') {
        setError(`${avgKg} kg de promedio está fuera de lo posible para este lote. Verifica el dato.`);
        return;
      }
      if (verdict === 'confirm') {
        setWeighingPendingConfirmation({
          sampleCount: weights.length,
          avgKg,
          minKg: Math.min(...weights),
          maxKg: Math.max(...weights),
          weights,
        });
        return;
      }
    }

    await submitWeighing(weights, false);
  };

  const recordDisposal = async () => {
    if (!cause) return;
    const count = Number(disposalCount);
    if (!Number.isFinite(count) || count <= 0) {
      setError('La cantidad debe ser mayor que cero.');
      return;
    }

    await run(
      () =>
        service.recordGroupEvent({
          groupId,
          eventType: 'Disposal',
          affectedCount: count,
          causeId: cause.causeId,
          payload: {},
        }),
      'Baja de lote registrada.',
    );
  };

  const recordTreatmentOrVaccination = async (eventType: 'Treatment' | 'Vaccination') => {
    if (!medication) return;
    const count = Number(headCount);
    if (!Number.isFinite(count) || count <= 0) {
      setError('El número de cabezas tratadas debe ser mayor que cero.');
      return;
    }

    await run(
      () =>
        service.recordGroupEvent({
          groupId,
          eventType,
          affectedCount: count,
          payload: { medicationId: medication.itemId, medicationName: medication.name, dose },
        }),
      eventType === 'Vaccination' ? 'Vacunación de lote registrada.' : 'Tratamiento de lote registrado.',
    );
  };

  const recordDiagnosis = async () => {
    const count = Number(affectedCount);
    if (!Number.isFinite(count) || count <= 0) {
      setError('La cantidad de cabezas afectadas debe ser mayor que cero.');
      return;
    }
    if (!condition.trim()) {
      setError('Describa la condición observada.');
      return;
    }

    await run(
      () =>
        service.recordGroupEvent({
          groupId,
          eventType: 'Diagnosis',
          affectedCount: count,
          // ADR-0015: "en este lote hay uno enfermo" — affected_count + condición, sin
          // identificar cuál animal. No hay picker de animal en este formulario a propósito.
          payload: { condition: condition.trim(), notes: notes.trim() || undefined },
        }),
      'Diagnóstico de lote registrado.',
    );
  };

  const recordFeed = async () => {
    if (!feedItem) return;
    const quantity = Number(feedQuantity.replace(',', '.'));
    if (!Number.isFinite(quantity) || quantity <= 0) {
      setError('La cantidad debe ser mayor que cero.');
      return;
    }

    await run(
      () =>
        feedService.recordFeedConsumption({
          groupId,
          inventoryItemId: feedItem.itemId,
          quantity,
          unit: feedUnit.trim() || undefined,
        }),
      'Consumo de alimento registrado.',
    );
  };

  if (!lot) {
    return (
      <Screen testID="lot-events-screen">
        <Title>Un lote</Title>
        <EmptyState testID="lot-events-missing" title="El lote seleccionado ya no está disponible." />
        <BigButton testID="lot-events-back" label="Volver" tone="neutral" onPress={onBack} />
      </Screen>
    );
  }

  const titleByActivity: Record<LotActivity, string> = {
    feed: 'Alimento (sacos)',
    weighing: 'Pesaje muestral',
    vaccination: 'Vacunar el lote',
    treatment: 'Tratar el lote',
    diagnosis: 'Hay uno enfermo',
    disposal: 'Baja con causa',
  };

  return (
    <Screen testID="lot-events-screen" scrollable>
      <Title>{titleByActivity[activity]}</Title>
      <Body muted>{lot.label}</Body>

      {error ? <Notice text={error} /> : null}
      {confirmation ? <Body muted>{confirmation}</Body> : null}

      <Card>
        {activity === 'weighing' ? (
          weighingPendingConfirmation ? (
            <>
              <Notice
                tone="warning"
                text={`${weighingPendingConfirmation.avgKg} kg de promedio es mucho más de lo normal para este lote. ¿Es correcto?`}
              />
              <BigButton
                testID="weighing-confirm-plausibility"
                label="Sí, registrar"
                busy={busy}
                onPress={() =>
                  void runOnce(() => submitWeighing(weighingPendingConfirmation.weights, true))
                }
              />
              <BigButton
                testID="weighing-cancel-plausibility"
                label="No, revisar"
                tone="neutral"
                onPress={() => setWeighingPendingConfirmation(null)}
              />
            </>
          ) : (
            <>
              <TextField
                testID="weighing-weights-input"
                label="Pesos (kg), separados por coma (ej: 45.5, 48, 50.2)"
                value={weightsText}
                onChangeText={setWeightsText}
              />
              <BigButton
                testID="confirm-weighing"
                label="Registrar pesaje muestral"
                busy={busy}
                onPress={() => void runOnce(recordWeighing)}
              />
            </>
          )
        ) : null}

        {activity === 'disposal' ? (
          !cause ? (
            mortalityCauses.length === 0 ? (
              <Body muted>
                No hay causas de mortalidad configuradas. Agréguelas desde el panel y
                sincronice para poder registrar la baja.
              </Body>
            ) : (
              mortalityCauses.map((option) => (
                <BigButton
                  key={option.causeId}
                  testID={`lot-cause-${option.causeId}`}
                  label={option.name}
                  tone="neutral"
                  onPress={() => setCause(option)}
                />
              ))
            )
          ) : (
            <>
              <Body muted>{cause.name}</Body>
              <NumberField
                testID="disposal-count-input"
                label="Cabezas"
                value={disposalCount}
                onChangeText={setDisposalCount}
              />
              <BigButton
                testID="confirm-disposal-lot"
                label="Registrar baja"
                busy={busy}
                onPress={() => void runOnce(recordDisposal)}
              />
            </>
          )
        ) : null}

        {activity === 'vaccination' || activity === 'treatment' ? (
          !medication ? (
            medications.length === 0 ? (
              <Body muted>
                No hay medicamentos en el inventario. Agréguelos desde el panel y
                sincronice para poder registrar {activity === 'vaccination' ? 'la vacunación' : 'el tratamiento'}.
              </Body>
            ) : (
              medications.map((option) => (
                <BigButton
                  key={option.itemId}
                  testID={`lot-medication-${option.itemId}`}
                  label={option.name}
                  tone="neutral"
                  onPress={() => setMedication(option)}
                />
              ))
            )
          ) : (
            <>
              <Body muted>{medication.name}</Body>
              <TextField testID="lot-dose-input" label="Dosis" value={dose} onChangeText={setDose} />
              <NumberField
                testID="lot-headcount-input"
                label="Cabezas tratadas"
                value={headCount}
                onChangeText={setHeadCount}
              />
              <BigButton
                testID="confirm-lot-treatment"
                label={activity === 'vaccination' ? 'Registrar vacunación' : 'Registrar tratamiento'}
                busy={busy}
                onPress={() =>
                  void runOnce(() =>
                    recordTreatmentOrVaccination(activity === 'vaccination' ? 'Vaccination' : 'Treatment'),
                  )
                }
              />
            </>
          )
        ) : null}

        {activity === 'diagnosis' ? (
          <>
            <NumberField
              testID="diagnosis-count-input"
              label="Cabezas afectadas"
              value={affectedCount}
              onChangeText={setAffectedCount}
            />
            <TextField
              testID="diagnosis-condition-input"
              label="Condición observada"
              value={condition}
              onChangeText={setCondition}
            />
            <TextField testID="diagnosis-notes-input" label="Notas (opcional)" value={notes} onChangeText={setNotes} />
            <BigButton
              testID="confirm-diagnosis"
              label="Registrar diagnóstico"
              busy={busy}
              onPress={() => void runOnce(recordDiagnosis)}
            />
          </>
        ) : null}

        {activity === 'feed' ? (
          !feedItem ? (
            feedItems.length === 0 ? (
              <Body muted>
                No hay alimentos en el inventario. Agréguelos desde el panel y sincronice
                para poder registrar el consumo.
              </Body>
            ) : (
              feedItems.map((option) => (
                <BigButton
                  key={option.itemId}
                  testID={`feed-item-${option.itemId}`}
                  label={option.name}
                  tone="neutral"
                  onPress={() => setFeedItem(option)}
                />
              ))
            )
          ) : (
            <>
              <Body muted>{feedItem.name}</Body>
              <NumberField
                testID="feed-quantity-input"
                label="Cantidad"
                value={feedQuantity}
                onChangeText={setFeedQuantity}
              />
              <TextField
                testID="feed-unit-input"
                label={`Unidad (opcional, ej. saco40kg; vacío = ${feedItem.unit})`}
                value={feedUnit}
                onChangeText={setFeedUnit}
              />
              <BigButton
                testID="confirm-feed"
                label="Registrar consumo"
                busy={busy}
                onPress={() => void runOnce(recordFeed)}
              />
            </>
          )
        ) : null}
      </Card>

      <BigButton testID="lot-events-back" label="Volver" tone="neutral" onPress={onBack} />
    </Screen>
  );
}

/**
 * Splits on comma/space (the separator between samples), so decimals must use a dot
 * ("45.5") — a decimal comma would be indistinguishable from the sample separator.
 */
function parseWeights(text: string): number[] {
  return text
    .split(/[,\s]+/)
    .map((piece) => piece.trim())
    .filter((piece) => piece.length > 0)
    .map((piece) => Number(piece))
    .filter((value) => Number.isFinite(value));
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}
