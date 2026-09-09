import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Database } from '@nozbe/watermelondb';

import { theme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  FormConfirmationSummary,
  FormHeader,
  Notice,
  NumberField,
  Screen,
  TextField,
  Title,
} from '../ui/components';
import type { EventService } from '../services/eventService';
import { evaluatePlausibility } from '../services/plausibilityService';
import { loadAdministrationRoutes, loadDoseKinds, loadTreatmentReasons } from '../services/herdQueries';
import { useDraftFlag } from '../ui/draftGuard';
import { useSingleFlight } from '../ui/useSingleFlight';

export interface TreatAnimalOption {
  animalId: string;
  label: string;
  speciesId?: string;
  categoryId?: string | null;
}

export interface TreatProductOption {
  itemId: string;
  name: string;
  unit: string;
}

/**
 * The curative path (docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md sec.C.2 task 6): "tengo un
 * animal enfermo, voy a tratarlo". Deeper than `VaccinateScreen` on purpose —
 * route, reason and dose vary case by case — but still bounded at **four
 * taps**: animal, product, the combined form (one pass, same accounting
 * `MilkingScreen` uses for "type the litres"), confirm.
 *
 * Route and reason default to sensible values (first active route, `curative`)
 * but stay editable in the same pass, which is what keeps this at four taps
 * instead of six: the sub-plan is explicit that asking for vía and motivo in a
 * *separate* pass would cost one tap too many.
 */
export function TreatScreen({
  service,
  database,
  animals,
  products,
  onRecorded,
  onCancel,
  initialAnimalId,
}: {
  service: EventService;
  database: Database;
  animals: TreatAnimalOption[];
  products: TreatProductOption[];
  onRecorded?: () => void;
  onCancel: () => void;
  initialAnimalId?: string;
}) {
  const [animal, setAnimal] = useState<TreatAnimalOption | null>(() =>
    initialAnimalId ? (animals.find((a) => a.animalId === initialAnimalId) ?? null) : null,
  );
  const [step, setStep] = useState<'animal' | 'product' | 'form' | 'confirm'>(() =>
    initialAnimalId ? 'product' : 'animal',
  );
  const [product, setProduct] = useState<TreatProductOption | null>(null);
  const [routes, setRoutes] = useState<{ routeId: string; key: string; labelEs: string }[]>([]);
  const [reasons, setReasons] = useState<{ reasonId: string; key: string; labelEs: string }[]>([]);
  const [absoluteDoseKindId, setAbsoluteDoseKindId] = useState<string | null>(null);
  const [routeId, setRouteId] = useState<string | null>(null);
  const [reasonKey, setReasonKey] = useState<string>('curative');
  const [dose, setDose] = useState('');
  const [notes, setNotes] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [pendingPlausibility, setPendingPlausibility] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saveNotice, setSaveNotice] = useState<string | null>(null);
  const [animalFilter, setAnimalFilter] = useState('');
  const [productFilter, setProductFilter] = useState('');
  /**
   * The latch is on `confirm`, which is the single writing path: "Confirmar"
   * and "Sí, registrar" are the same function with a different argument, so one
   * `runOnce` per press covers both without ever nesting. It has to start at the
   * press and not at the enqueue, because on a typed dose `confirm` reads the
   * plausibility ranges first — a `busy` flag flipped after that await left the
   * button live for the whole read, which is exactly where a gloved double tap
   * lands.
   */
  const { busy, runOnce } = useSingleFlight();

  /**
   * D3 — everything past the empty first step is work the employee already did:
   * the animal they walked out to find, the dose they read off the syringe, the
   * note about what the cow looked like. Losing that to a mistaken "Inicio" is
   * exactly what the shell's question exists to prevent.
   *
   * The untouched animal picker is deliberately *not* a draft. A screen that
   * asks on every exit, including the one the employee only opened by mistake,
   * teaches them to tap "Salir y descartar" without reading it — and then the
   * question stops protecting the treatment it was written for.
   */
  useDraftFlag(animal !== null || dose.trim().length > 0 || notes.trim().length > 0);

  useEffect(() => {
    void (async () => {
      const [loadedRoutes, loadedReasons, doseKinds] = await Promise.all([
        loadAdministrationRoutes(database),
        loadTreatmentReasons(database),
        loadDoseKinds(database),
      ]);
      setRoutes(loadedRoutes);
      setReasons(loadedReasons);
      setRouteId((current) => current ?? loadedRoutes[0]?.routeId ?? null);
      setAbsoluteDoseKindId(doseKinds.find((k) => k.key === 'absolute')?.doseKindId ?? null);
    })();
  }, [database]);

  const reset = () => {
    setStep('animal');
    setAnimal(null);
    setProduct(null);
    setDose('');
    setNotes('');
    setFormError(null);
    setPendingPlausibility(null);
    setError(null);
    setReasonKey('curative');
    setAnimalFilter('');
    setProductFilter('');
  };

  const cancel = () => {
    setSaveNotice(null);
    reset();
    onCancel();
  };

  // 3 — the combined form: route + reason + dose + notes in one pass. Advances
  // to the review/confirm card; nothing is enqueued yet.
  const continueToConfirm = () => {
    setFormError(null);

    if (!routeId) {
      setFormError('Selecciona una vía de administración.');
      return;
    }

    const trimmedDose = dose.trim();
    if (trimmedDose.length === 0 && notes.trim().length === 0) {
      setFormError('Sin una dosis numérica, las notas son obligatorias.');
      return;
    }

    if (trimmedDose.length > 0) {
      const doseNum = Number(trimmedDose.replace(',', '.'));
      if (!Number.isFinite(doseNum) || doseNum <= 0) {
        setFormError('La dosis debe ser un número mayor que 0.');
        return;
      }
    }

    setStep('confirm');
  };

  /**
   * T5.1 / D3 — the way back from the review card. Until now the only control
   * that left it was "Cancelar", which runs `reset()`: an employee who reached
   * the confirmation and noticed the dose said 5 instead of 50 had to throw away
   * the animal, the product, the vía, the motivo and the notes to fix one field.
   * This only moves the step; no form state is touched, which is the whole point.
   */
  const backToForm = () => {
    setStep('form');
    setFormError(null);
  };

  const isAnimalObsolete = Boolean(animal && !animals.some((a) => a.animalId === animal.animalId));

  // 4 — confirm (or, on an improbable dose, the plausibility dialog stands in
  // for it and a second press finishes the job — ADR-0022 sec.5, same pattern
  // as EventsScreen/MilkingScreen).
  const confirm = async (isPlausibilityConfirmed = false) => {
    if (!animal || !routeId || !absoluteDoseKindId || isAnimalObsolete) return;

    setError(null);
    const trimmedDose = dose.trim();
    const doseNum = trimmedDose.length > 0 ? Number(trimmedDose.replace(',', '.')) : null;

    if (doseNum !== null && !isPlausibilityConfirmed && animal.speciesId) {
      const verdict = await evaluatePlausibility(database, {
        speciesId: animal.speciesId,
        categoryId: animal.categoryId ?? null,
        magnitude: 'dose_ml',
        value: doseNum,
      });
      if (verdict === 'block') {
        setError('La dosis está fuera del rango absoluto para esta especie. Verifica el dato.');
        return;
      }
      if (verdict === 'confirm') {
        setPendingPlausibility(doseNum);
        return;
      }
    }

    try {
      const unit = product?.unit ?? 'unidad';
      await service.recordTreatmentCourse({
        animalId: animal.animalId,
        routeId,
        reason: reasonKey,
        productId: product?.itemId,
        doseKindId: absoluteDoseKindId,
        doseFactorAmount: doseNum ?? 1,
        doseFactorUnit: unit,
        notes: notes.trim() || undefined,
        administeredDoseAmount: doseNum ?? undefined,
        administeredDoseUnit: doseNum !== null ? unit : undefined,
        applicationNotes: notes.trim() || undefined,
        isPlausibilityConfirmed,
      });
      const recordedLabel = animal.label;
      reset();
      setSaveNotice(`Tratamiento de ${recordedLabel} guardado en este teléfono. Pendiente de enviar.`);
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    }
  };

  const filteredAnimals = animals.filter((a) => {
    const q = animalFilter.trim().toLowerCase();
    if (!q) return true;
    return a.label.toLowerCase().includes(q) || a.animalId.toLowerCase().includes(q);
  });

  const filteredProducts = products.filter((p) => {
    const q = productFilter.trim().toLowerCase();
    if (!q) return true;
    return p.name.toLowerCase().includes(q) || p.itemId.toLowerCase().includes(q);
  });

  // D1 — one vertical gesture per render. The picker steps own a bounded inner
  // ScrollView, so the Screen stays fixed under them; nesting a second vertical
  // scroller there would make the two fight for the same drag and the inner one
  // traps it. The form and confirm steps own none, and they are precisely the
  // ones that overflow: with the stress catalog (8 vías + 6 motivos) the two
  // pickers alone are 14 × 64 units, before the dose, the notes and "Continuar".
  const scrollable = !((step === 'animal' && animals.length > 0) || step === 'product');

  return (
    <Screen testID="treat-screen" scrollable={scrollable}>
      <FormHeader
        title={step === 'animal' ? 'Tratar animal enfermo' : 'Tratamiento'}
        subtitle={product ? `${product.name}${dose ? ` · ${dose} ${product.unit}` : ''}` : undefined}
        animalLabel={animal?.label}
        onCancel={cancel}
        testID="treat-header"
      />
      <Title>Tratar animal enfermo</Title>

      {error ? <Notice text={error} /> : null}
      {saveNotice && step === 'animal' ? (
        <Card testID="treat-save-notice" style={{ borderColor: theme.color.primary }}>
          <Body>✓ {saveNotice}</Body>
          <Body muted>Puede continuar registrando en otro animal sin duplicar este hecho.</Body>
        </Card>
      ) : null}

      {/*
        `flex: 1` is what bounds the picker steps' inner scroller to the window.
        Inside a scrollable content box that same clamp caps the form at one
        window and puts "Continuar" back out of reach with nothing to scroll, so
        the scrollable branch only grows.
      */}
      <View style={scrollable ? styles.bodyGrow : styles.body}>
        {step === 'animal' ? (
          animals.length === 0 ? (
            <EmptyState
              testID="treat-animal-empty"
              title="No hay animales en el dispositivo"
              hint="Vaya a Inicio → Sincronización para descargar el hato."
            />
          ) : (
            <>
              {animals.length > 5 ? (
                <TextField
                  testID="treat-animal-search"
                  label="Buscar animal"
                  placeholder="Buscar por arete o nombre..."
                  value={animalFilter}
                  onChangeText={setAnimalFilter}
                />
              ) : null}
              <ScrollView testID="treat-animal-list" contentContainerStyle={styles.list}>
                {filteredAnimals.map((option) => (
                  <BigButton
                    key={option.animalId}
                    testID={`treat-animal-${option.animalId}`}
                    label={option.label}
                    tone="neutral"
                    onPress={() => {
                      setAnimal(option);
                      setSaveNotice(null);
                      setStep('product');
                    }}
                  />
                ))}
              </ScrollView>
            </>
          )
        ) : null}

        {step === 'product' ? (
          <>
            {products.length > 5 ? (
              <TextField
                testID="treat-product-search"
                label="Buscar producto"
                placeholder="Buscar por nombre..."
                value={productFilter}
                onChangeText={setProductFilter}
              />
            ) : null}
            <ScrollView testID="treat-product-list" contentContainerStyle={styles.list}>
              {filteredProducts.map((option) => (
                <BigButton
                  key={option.itemId}
                  testID={`treat-product-${option.itemId}`}
                  label={option.name}
                  tone="neutral"
                  onPress={() => {
                    setProduct(option);
                    setStep('form');
                  }}
                />
              ))}
              <BigButton
                testID="treat-product-skip"
                label="Sin producto de inventario"
                tone="neutral"
                onPress={() => setStep('form')}
              />
            </ScrollView>
          </>
        ) : null}

        {step === 'form' && animal ? (
          <Card>
            <Body>{animal.label}</Body>
            {product ? <Body muted>{product.name}</Body> : null}

            <Body muted>Vía de administración</Body>
            <View testID="treat-route-picker">
              {routes.map((r) => (
                <BigButton
                  key={r.routeId}
                  label={routeId === r.routeId ? `✓ ${r.labelEs}` : r.labelEs}
                  tone={routeId === r.routeId ? 'primary' : 'neutral'}
                  onPress={() => setRouteId(r.routeId)}
                />
              ))}
            </View>

            <Body muted>Motivo</Body>
            <View testID="treat-reason-picker">
              {reasons.map((r) => (
                <BigButton
                  key={r.reasonId}
                  label={reasonKey === r.key ? `✓ ${r.labelEs}` : r.labelEs}
                  tone={reasonKey === r.key ? 'primary' : 'neutral'}
                  onPress={() => setReasonKey(r.key)}
                />
              ))}
            </View>

            <NumberField
              label={`Dosis administrada${product ? ` (${product.unit})` : ''}`}
              testID="treat-dose-input"
              value={dose}
              onChangeText={setDose}
              unit={product?.unit}
              hint="Dosis en la unidad del producto"
              error={formError && formError.includes('dosis') ? 'Dosis inválida' : undefined}
            />
            <TextField
              label="Notas"
              testID="treat-notes-input"
              value={notes}
              onChangeText={setNotes}
              hint="Obligatorio si no ingresa dosis numérica"
              optional
              error={formError && formError.includes('notas') ? 'Notas requeridas' : undefined}
            />

            {formError ? <Notice text={formError} /> : null}

            <BigButton
              testID="treat-continue"
              label="Continuar"
              onPress={continueToConfirm}
            />
          </Card>
        ) : null}

        {step === 'confirm' && animal ? (
          <Card>
            <FormConfirmationSummary
              title="Revisión de tratamiento"
              subjectType="animal"
              subjectLabel={animal.label}
              date={new Date().toISOString().slice(0, 10)}
              fields={[
                { label: 'Producto', value: product ? product.name : 'Sin producto de inventario', symbol: '💊' },
                { label: 'Vía', value: routes.find((r) => r.routeId === routeId)?.labelEs ?? '—', symbol: '📋' },
                { label: 'Motivo', value: reasons.find((r) => r.key === reasonKey)?.labelEs ?? reasonKey, symbol: '❓' },
                {
                  label: 'Dosis',
                  value: dose.trim() ? dose : 'Sin dosis numérica',
                  unit: dose.trim() && product ? product.unit : undefined,
                  symbol: '⚖️',
                },
              ]}
              notes={notes.trim() || undefined}
              testID="treat-confirm-summary"
            />

            <Body>{animal.label}</Body>

            {isAnimalObsolete ? (
              <>
                <Notice
                  tone="warning"
                  text="El animal seleccionado ya no existe en el sistema (fue eliminado o dado de baja en el servidor). No se puede registrar el tratamiento contra este animal. Puede elegir otro animal sin perder los datos ingresados."
                />
                <BigButton
                  testID="change-animal"
                  label="Elegir otro animal"
                  tone="neutral"
                  onPress={() => {
                    setAnimal(null);
                    setStep('animal');
                    setError(null);
                  }}
                />
              </>
            ) : null}

            <Body muted>
              {dose.trim() ? `Dosis: ${dose}${product ? ` ${product.unit}` : ''}` : 'Sin dosis numérica'}
            </Body>

            {pendingPlausibility !== null ? (
              <>
                <Notice
                  tone="warning"
                  text={`${pendingPlausibility} es mucho más de lo normal para este animal. ¿Es correcto?`}
                />
                <BigButton
                  testID="treat-confirm-plausibility"
                  label="Sí, registrar"
                  busy={busy}
                  disabled={isAnimalObsolete}
                  onPress={() => void runOnce(() => confirm(true))}
                />
                <BigButton
                  testID="treat-cancel-plausibility"
                  label="No, revisar"
                  tone="neutral"
                  onPress={() => setPendingPlausibility(null)}
                />
              </>
            ) : (
              <>
                <BigButton
                  testID="treat-confirm"
                  label="Confirmar"
                  busy={busy}
                  disabled={isAnimalObsolete}
                  onPress={() => void runOnce(() => confirm(false))}
                />
                <BigButton
                  testID="treat-back-to-form"
                  label="Corregir datos"
                  tone="neutral"
                  onPress={backToForm}
                />
              </>
            )}
          </Card>
        ) : null}
      </View>

      <BigButton testID="treat-cancel" label="Cancelar" tone="neutral" onPress={cancel} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  body: { flex: 1 },
  bodyGrow: { flexGrow: 1 },
  list: { gap: theme.space.sm, paddingBottom: theme.space.md },
});
