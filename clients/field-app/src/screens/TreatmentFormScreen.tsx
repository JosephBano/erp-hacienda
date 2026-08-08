import { useState, useEffect, useCallback } from 'react';
import { View, ScrollView } from 'react-native';
import { Database } from '@nozbe/watermelondb';

import { BigButton, Card, Notice, Screen, Title, Body, TextField, NumberField } from '../ui/components';
import { evaluatePlausibility, PlausibilityVerdict } from '../services/plausibilityService';
import { loadAdministrationRoutes, loadTreatmentReasons } from '../services/herdQueries';
import { Outbox } from '../services/outbox';

/**
 * The shared form-state shape used by both VaccinateScreen and
 * TreatScreen. The two screens differ in default values and which
 * fields the user must confirm; the underlying state and submission
 * contract are identical (sub-plan 3.5a.2-C sec.C.2).
 */
export interface TreatmentFormState {
  routeId: string;
  reasonId: string;
  doseKg: string;
  notes: string;
  confirmed: boolean;
}

export const initialTreatmentFormState: TreatmentFormState = {
  routeId: '',
  reasonId: '',
  doseKg: '',
  notes: '',
  confirmed: false,
};

export interface TreatmentScreenProps {
  database: Database;
  outbox: Outbox;
  animalId: string;
  animalSpeciesId: string;
  animalCategoryId?: string | null;
  /**
   * 'vaccination' or 'treatment'. Drives the screen title and which
   * defaults apply (vaccination defaults reason='scheduled' and skips
   * the route picker; treatment shows both pickers and accepts notes).
   */
  mode: 'vaccination' | 'treatment';
  /**
   * The recommended inventory item (vaccine or medication) to prefill.
   * The product name and a unit are shown above the dose input as
   * context, never as the registered value (the operator types it).
   */
  productName?: string;
  onRecorded: () => void;
  onCancel: () => void;
}

/**
 * The shared form for the two screens. The state machine is the same
 * for both; only the defaults and the validation rules differ.
 *
 * Tapping "Confirmar" enqueues a `recordAnimalEvent` (or `vaccination`
 * / `treatment`, the sync layer keeps both operational types) into the
 * local outbox. The plausibility check (ADR-0022) runs once, just
 * before the enqueue, and a "confirm" verdict raises
 * `isConfirmed = true` on the payload if the user accepts the
 * dialog.
 */
export function TreatmentFormScreen(props: TreatmentScreenProps) {
  const { database, outbox, animalId, animalSpeciesId, animalCategoryId, mode, productName, onRecorded, onCancel } = props;

  const [state, setState] = useState<TreatmentFormState>(initialTreatmentFormState);
  const [routes, setRoutes] = useState<{ routeId: string; key: string; labelEs: string }[]>([]);
  const [reasons, setReasons] = useState<{ reasonId: string; key: string; labelEs: string }[]>([]);
  const [plausibility, setPlausibility] = useState<PlausibilityVerdict | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void (async () => {
      setRoutes(await loadAdministrationRoutes(database));
      setReasons(await loadTreatmentReasons(database));
    })();
  }, [database]);

  const update = useCallback(
    <K extends keyof TreatmentFormState>(key: K, value: TreatmentFormState[K]) => {
      setState((prev) => ({ ...prev, [key]: value }));
    },
    [],
  );

  const onConfirm = useCallback(async () => {
    setError(null);

    if (!state.routeId) {
      setError('Selecciona una vía de administración.');
      return;
    }
    if (!state.reasonId) {
      setError('Selecciona un motivo de tratamiento.');
      return;
    }

    // Plausibility: weight_kg for the dose (treated as 0 by the validator
    // if empty, so we skip when blank).
    const doseNum = Number(state.doseKg.replace(',', '.'));
    if (state.doseKg.trim().length > 0) {
      if (!Number.isFinite(doseNum) || doseNum <= 0) {
        setError('La dosis debe ser un número mayor que 0.');
        return;
      }
      const verdict = await evaluatePlausibility(database, {
        speciesId: animalSpeciesId,
        categoryId: animalCategoryId ?? null,
        magnitude: 'dose_ml',
        value: doseNum,
      });
      if (verdict === 'block') {
        setError('La dosis está fuera del rango absoluto para esta especie.');
        return;
      }
      if (verdict === 'confirm' && !state.confirmed) {
        setPlausibility('confirm');
        return;
      }
    }

    setBusy(true);
    try {
      const payload = {
        animalId,
        eventType: mode === 'vaccination' ? 'Vaccination' : 'Treatment',
        occurredAt: new Date().toISOString(),
        recordedBy: 'field-app',
        routeId: state.routeId,
        reasonId: state.reasonId,
        doseKg: state.doseKg.trim() || null,
        notes: state.notes.trim() || null,
        isPlausibilityConfirmed: state.confirmed,
      };
      await outbox.enqueue('recordAnimalEvent', payload, payload.occurredAt);
      setState(initialTreatmentFormState);
      setPlausibility(null);
      onRecorded();
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setBusy(false);
    }
  }, [animalCategoryId, animalId, animalSpeciesId, database, mode, onRecorded, outbox, state]);

  return (
    <Screen>
      <ScrollView>
        <Title>{mode === 'vaccination' ? 'Registrar vacunación' : 'Registrar tratamiento'}</Title>
        {productName && <Body>Producto: {productName}</Body>}

        <Card>
          <Body>Vía de administración</Body>
          <Picker
            value={state.routeId}
            onChange={(v) => update('routeId', v)}
            options={routes.map((r) => ({ value: r.routeId, label: r.labelEs }))}
            testID="treatment-route-picker"
          />
        </Card>

        <Card>
          <Body>Motivo</Body>
          <Picker
            value={state.reasonId}
            onChange={(v) => update('reasonId', v)}
            options={reasons.map((r) => ({ value: r.reasonId, label: r.labelEs }))}
            testID="treatment-reason-picker"
          />
        </Card>

        <Card>
          <Body>Dosis (opcional)</Body>
          <NumberField
            label="Dosis"
            value={state.doseKg}
            onChangeText={(t) => update('doseKg', t)}
            testID="treatment-dose-input"
          />
        </Card>

        <Card>
          <Body>Notas (opcional)</Body>
          <TextField
            label="Notas"
            value={state.notes}
            onChangeText={(t) => update('notes', t)}
            testID="treatment-notes-input"
          />
        </Card>

        {error && <Notice text={error} tone="danger" />}

        {plausibility === 'confirm' && (
          <Card>
            <Body>
              La dosis parece inusualmente grande o pequeña para esta especie. ¿Es correcto?
            </Body>
            <BigButton
              label="Sí, registrar"
              tone="primary"
              onPress={() => {
                setState((prev) => ({ ...prev, confirmed: true }));
                setPlausibility(null);
                void onConfirm();
              }}
              testID="treatment-confirm-plausibility"
            />
            <BigButton
              label="No, revisar"
              tone="neutral"
              onPress={() => setPlausibility(null)}
              testID="treatment-cancel-plausibility"
            />
          </Card>
        )}

        <View style={{ flexDirection: 'row', gap: 12 }}>
          <BigButton
            label="Cancelar"
            tone="neutral"
            onPress={onCancel}
            testID="treatment-cancel"
          />
          <BigButton
            label="Confirmar"
            tone="primary"
            busy={busy}
            onPress={onConfirm}
            testID="treatment-confirm"
          />
        </View>
      </ScrollView>
    </Screen>
  );
}

/**
 * Tiny inline picker. The field app does not pull in a form library
 * (Art. 17: no technology by curiosity); the picker is a controlled
 * View that just renders one <BigButton> per option, since the lists
 * here are small (≤10 items).
 */
function Picker(props: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  testID?: string;
}) {
  return (
    <View testID={props.testID}>
      {props.options.map((o) => (
        <BigButton
          key={o.value}
          label={props.value === o.value ? `✓ ${o.label}` : o.label}
          tone={props.value === o.value ? 'primary' : 'neutral'}
          onPress={() => props.onChange(o.value)}
        />
      ))}
    </View>
  );
}
