import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Database } from '@nozbe/watermelondb';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, Screen, Title } from '../ui/components';
import type { EventService } from '../services/eventService';
import { loadAdministrationRoutes, loadDoseKinds } from '../services/herdQueries';

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
 * Vaccination as its own path (PLAN-FASE-3-5-PORCINO-3.5a.2-C sec.C.2 task 5):
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
}: {
  service: EventService;
  /** Only used to load the local route/dose-kind catalogs (Art. 9: no network). */
  database: Database;
  animals: VaccinateAnimalOption[];
  products: VaccinateProductOption[];
  onRecorded?: () => void;
  onCancel: () => void;
}) {
  const [step, setStep] = useState<'animal' | 'product' | 'confirm'>('animal');
  const [animal, setAnimal] = useState<VaccinateAnimalOption | null>(null);
  const [product, setProduct] = useState<VaccinateProductOption | null>(null);
  const [routeId, setRouteId] = useState<string | null>(null);
  const [doseKindId, setDoseKindId] = useState<string | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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

  // 3 — confirm. No plausibility check here: the dose is a fixed "one unit
  // per head" default, never operator-typed, so there is no value to
  // evaluate against `plausibility_ranges` (ADR-0022 only gates values a
  // human entered).
  const confirm = async () => {
    if (!animal || !product || !routeId || !doseKindId) return;

    setBusy(true);
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
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="vaccinate-screen">
      <Title>Vacunar</Title>

      {catalogError ? <Notice text={catalogError} tone="warning" /> : null}
      {error ? <Notice text={error} /> : null}

      <View style={styles.body}>
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
                    setStep('product');
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
            <Body muted>{`${product.name} · 1 ${product.unit} por cabeza`}</Body>
            <BigButton
              testID="vaccinate-confirm"
              label="Confirmar"
              busy={busy}
              disabled={!routeId || !doseKindId}
              onPress={() => void confirm()}
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
  list: { gap: theme.space.sm, paddingBottom: theme.space.md },
});
