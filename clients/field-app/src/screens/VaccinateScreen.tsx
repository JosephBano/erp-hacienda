import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Database } from '@nozbe/watermelondb';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, Screen, Title } from '../ui/components';
import type { EventService } from '../services/eventService';
import { loadAdministrationRoutes, loadDoseKinds } from '../services/herdQueries';
import { useSingleFlight } from '../ui/useSingleFlight';

export interface VaccinateAnimalOption {
  animalId: string;
  label: string;
  speciesId?: string;
  categoryId?: string | null;
}

export interface VaccinateProductOption {
  itemId: string;
  name: string;
  unit: string;
}

/**
 * Vaccination as its own path (docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md sec.C.2 task 5):
 * "voy a aplicar el cronograma de vacunas de hoy" is the common case and it
 * costs exactly **three taps** — animal, product, confirm. Reason is fixed to
 * `scheduled`, dose form is fixed to `per_head` (one dose of the product per
 * animal), and route defaults to the first active row of the local catalog:
 * none of those three are asked again, which is what keeps this at three taps
 * instead of the five-plus of `TreatScreen`.
 *
 * The route default is a stopgap: `InventoryItem` does not yet carry a
 * "preferred route" for a product (that would live in the Inventory module,
 * out of scope for this sub-branch — Art. 6). Tracked in BACKLOG.md.
 */
export function VaccinateScreen({
  service,
  database,
  animals,
  products,
  onRecorded,
  onCancel,
  initialAnimalId,
}: {
  service: EventService;
  /** Only used to load the local route/dose-kind catalogs (Art. 9: no network). */
  database: Database;
  animals: VaccinateAnimalOption[];
  products: VaccinateProductOption[];
  onRecorded?: () => void;
  onCancel: () => void;
  initialAnimalId?: string;
}) {
  const [animal, setAnimal] = useState<VaccinateAnimalOption | null>(() =>
    initialAnimalId ? (animals.find((a) => a.animalId === initialAnimalId) ?? null) : null,
  );
  const [step, setStep] = useState<'animal' | 'product' | 'confirm'>(() =>
    initialAnimalId ? 'product' : 'animal',
  );
  const [product, setProduct] = useState<VaccinateProductOption | null>(null);
  const [routeId, setRouteId] = useState<string | null>(null);
  const [doseKindId, setDoseKindId] = useState<string | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  /**
   * One writing path, one latch: `confirm` is the only thing on this screen that
   * touches the outbox. `busy` alone never guarded it — React schedules the flag
   * rather than applying it, so both halves of a double tap read it as false and
   * both enqueue a vaccination for an animal that got one dose.
   */
  const { busy, runOnce } = useSingleFlight();

  /*
   * No `useDraftFlag` here, on purpose (D3, T5.2).
   *
   * This screen has no field the employee types into: everything it holds is
   * two picks off a list, and the vía, the motivo and the dose are fixed
   * defaults it never asks for. Leaving at the confirm card costs two taps to
   * redo and loses no information the employee had to remember — unlike a dose
   * read off a syringe in the rain, which is what the shell's question is for.
   *
   * Declaring a draft here would spend that question on the cheapest screen in
   * the app, and a question that fires on a screen with nothing written on it is
   * how employees learn to tap "Salir y descartar" without reading it. The way
   * to protect the two picks is to make going back keep them, which is what
   * "Elegir otro producto" below does.
   */

  useEffect(() => {
    void (async () => {
      const [routes, doseKinds] = await Promise.all([
        loadAdministrationRoutes(database),
        loadDoseKinds(database),
      ]);
      setRouteId(routes[0]?.routeId ?? null);
      setDoseKindId(doseKinds.find((k) => k.key === 'per_head')?.doseKindId ?? null);
      if (routes.length === 0 || doseKinds.find((k) => k.key === 'per_head') === undefined) {
        setCatalogError(
          'Falta el catálogo de vías o formas de dosis. Sincronice antes de registrar vacunaciones.',
        );
      }
    })();
  }, [database]);

  const reset = () => {
    setStep('animal');
    setAnimal(null);
    setProduct(null);
    setError(null);
  };

  const cancel = () => {
    reset();
    onCancel();
  };

  // 2 — choosing the product also resolves every remaining default, so the
  // very next thing the operator sees is the confirm card (tap 3).
  const chooseProduct = (chosen: VaccinateProductOption) => {
    setProduct(chosen);
    setStep('confirm');
  };

  const isAnimalObsolete = Boolean(animal && !animals.some((a) => a.animalId === animal.animalId));

  // 3 — confirm. No plausibility check here: the dose is a fixed "one unit
  // per head" default, never operator-typed, so there is no value to
  // evaluate against `plausibility_ranges` (ADR-0022 only gates values a
  // human entered).
  const confirm = async () => {
    if (!animal || !product || !routeId || !doseKindId || isAnimalObsolete) return;

    setError(null);
    try {
      await service.recordTreatmentCourse({
        animalId: animal.animalId,
        routeId,
        reason: 'scheduled',
        productId: product.itemId,
        doseKindId,
        doseFactorAmount: 1,
        doseFactorUnit: product.unit,
        notes: `Vacunación: ${product.name}.`,
      });
      reset();
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    }
  };

  // D1 — one vertical gesture per render. Both picker steps own a bounded inner
  // ScrollView (unless the catalog is empty and they fall back to an EmptyState),
  // so the Screen stays fixed under them rather than nesting a second scroller
  // that would fight for the same drag. The confirm card owns none: with a long
  // animal label, the catalog warning and the error notice it is what overflows
  // on a short screen, and "Confirmar" is the control that goes out of reach.
  const scrollable = !(
    (step === 'animal' && animals.length > 0) || (step === 'product' && products.length > 0)
  );

  return (
    <Screen testID="vaccinate-screen" scrollable={scrollable}>
      <Title>Vacunar</Title>

      {catalogError ? <Notice text={catalogError} tone="warning" /> : null}
      {error ? <Notice text={error} /> : null}

      {/*
        `flex: 1` is what bounds the picker steps' inner scroller to the window.
        Inside a scrollable content box that same clamp caps the content at one
        window and puts "Confirmar" back out of reach with nothing to scroll, so
        the scrollable branch only grows.
      */}
      <View style={scrollable ? styles.bodyGrow : styles.body}>
        {step === 'animal' ? (
          animals.length === 0 ? (
            <EmptyState
              testID="vaccinate-animal-empty"
              title="No hay animales en el dispositivo"
              hint="Vaya a Inicio → Sincronización para descargar el hato."
            />
          ) : (
            <ScrollView testID="vaccinate-animal-list" contentContainerStyle={styles.list}>
              {animals.map((option) => (
                <BigButton
                  key={option.animalId}
                  testID={`vaccinate-animal-${option.animalId}`}
                  label={option.label}
                  tone="neutral"
                  onPress={() => {
                    setAnimal(option);
                    setStep(product ? 'confirm' : 'product');
                  }}
                />
              ))}
            </ScrollView>
          )
        ) : null}

        {step === 'product' ? (
          products.length === 0 ? (
            <EmptyState
              testID="vaccinate-product-empty"
              title="No hay productos en el inventario"
              hint="Agregue vacunas desde el panel y sincronice."
            />
          ) : (
            <ScrollView testID="vaccinate-product-list" contentContainerStyle={styles.list}>
              {products.map((option) => (
                <BigButton
                  key={option.itemId}
                  testID={`vaccinate-product-${option.itemId}`}
                  label={option.name}
                  tone="neutral"
                  onPress={() => chooseProduct(option)}
                />
              ))}
            </ScrollView>
          )
        ) : null}

        {step === 'confirm' && animal && product ? (
          <Card>
            <Body>{animal.label}</Body>

            {isAnimalObsolete ? (
              <>
                <Notice
                  tone="warning"
                  text="El animal seleccionado ya no existe en el sistema (fue eliminado o dado de baja en el servidor). No se puede registrar la vacunación contra este animal. Puede elegir otro animal sin perder los datos ingresados."
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

            <Body muted>{`${product.name} · 1 ${product.unit} por cabeza`}</Body>
            <BigButton
              testID="vaccinate-confirm"
              label="Confirmar"
              busy={busy}
              disabled={!routeId || !doseKindId || isAnimalObsolete}
              onPress={() => void runOnce(confirm)}
            />
            {/*
              T5.1 / D3 — the way back from the review card. "Cancelar" was the
              only control that left it and it runs `reset()`, so an employee who
              had picked the wrong vaccine lost the animal too and started the
              three taps over. This moves the step and nothing else: the animal
              stays chosen.
            */}
            <BigButton
              testID="vaccinate-back-to-product"
              label="Elegir otro producto"
              tone="neutral"
              onPress={() => setStep('product')}
            />
          </Card>
        ) : null}
      </View>

      <BigButton testID="vaccinate-cancel" label="Cancelar" tone="neutral" onPress={cancel} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  body: { flex: 1 },
  bodyGrow: { flexGrow: 1 },
  list: { gap: theme.space.sm, paddingBottom: theme.space.md },
});
