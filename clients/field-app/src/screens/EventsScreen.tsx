import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';

import { theme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  NumberField,
  Screen,
  TextField,
  Title,
} from '../ui/components';
import type { EventService } from '../services/eventService';

export interface AnimalOption {
  animalId: string;
  label: string;
  /** Resolved automatically for "Baja con causa" (3.5a.3) while the animal is still its own row. */
  motherId?: string;
}

export interface GroupOption {
  groupId: string;
  label: string;
}

export interface MedicationOption {
  itemId: string;
  name: string;
  milkWithdrawalDays?: number;
  meatWithdrawalDays?: number;
}

export interface MortalityCauseOption {
  causeId: string;
  name: string;
}

/**
 * The five states the screen can be in. 'menu' is the picker of activity types;
 * the other four are the dedicated forms. Pre-selection from the activity tree
 * (3.5a.9-B) lands directly in one of them, skipping the menu.
 */
export type EventMode = 'menu' | 'treatment' | 'weight' | 'move' | 'disposal';

/** Treatments, weighings, lot moves and individual disposals — recorded from the paddock. */
export function EventsScreen({
  service,
  animals,
  groups,
  medications,
  mortalityCauses,
  onRecorded,
  initialAnimalId,
  initialActivity,
}: {
  service: EventService;
  animals: AnimalOption[];
  groups: GroupOption[];
  medications: MedicationOption[];
  mortalityCauses?: MortalityCauseOption[];
  onRecorded?: () => void;
  /**
   * Caller-supplied animal + activity to skip the picker steps. The activity tree
   * (3.5a.9-B) hands these in so the operator does not re-pick what they already
   * chose two screens ago. Undefined means: show the full menu (legacy path).
   */
  initialAnimalId?: string;
  initialActivity?: 'treatment' | 'weight' | 'move' | 'disposal';
}) {
  const [mode, setMode] = useState<EventMode>('menu');
  const [animal, setAnimal] = useState<AnimalOption | null>(null);
  const [medication, setMedication] = useState<MedicationOption | null>(null);
  const [dose, setDose] = useState('');
  const [weight, setWeight] = useState('');
  const [cause, setCause] = useState<MortalityCauseOption | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  /**
   * Pre-selection bridge. When the activity tree hands us an animal, we set it on
   * mount so the picker is replaced by the form. When an activity is also handed
   * in, the menu disappears too: the operator has already chosen, the form is what
   * they want next.
   */
  useEffect(() => {
    if (initialAnimalId) {
      const match = animals.find((a) => a.animalId === initialAnimalId);
      if (match) setAnimal(match);
    }
    if (initialActivity) {
      setMode(initialActivity);
    }
    // We only want this to fire on mount; if the parent re-renders with new
    // animals/initialAnimalId the user has already pressed "Volver" or recorded
    // and a re-selection would be a surprise.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const reset = () => {
    setMode('menu');
    setAnimal(null);
    setMedication(null);
    setDose('');
    setWeight('');
    setCause(null);
    setError(null);
  };

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      setConfirmation(done);
      reset();
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (mode === 'menu') {
    return (
      <Screen testID="events-screen">
        <Title>Registrar evento</Title>
        {confirmation ? <Body muted>{confirmation}</Body> : null}
        <BigButton testID="mode-treatment" label="Tratamiento" onPress={() => setMode('treatment')} />
        <BigButton testID="mode-weight" label="Pesaje" tone="neutral" onPress={() => setMode('weight')} />
        <BigButton testID="mode-move" label="Cambio de lote" tone="neutral" onPress={() => setMode('move')} />
        <BigButton testID="mode-disposal" label="Baja con causa" tone="neutral" onPress={() => setMode('disposal')} />
      </Screen>
    );
  }

  const titleByMode: Record<Exclude<EventMode, 'menu'>, string> = {
    treatment: 'Tratamiento',
    weight: 'Pesaje',
    move: 'Cambio de lote',
    disposal: 'Baja con causa',
  };

  return (
    <Screen testID="events-screen">
      <Title>{titleByMode[mode as Exclude<EventMode, 'menu'>]}</Title>

      {error ? <Notice text={error} /> : null}

      <View style={styles.body}>
        {!animal ? (
          animals.length === 0 ? (
            <EmptyState
              testID="events-animal-empty"
              title="No hay animales en el dispositivo"
              hint="Vaya a Inicio → Sincronización para descargar el hato antes de registrar eventos."
            />
          ) : (
            <ScrollView testID="animal-list" contentContainerStyle={styles.list}>
              {animals.map((option) => (
                <BigButton
                  key={option.animalId}
                  testID={`animal-${option.animalId}`}
                  label={option.label}
                  tone="neutral"
                  onPress={() => setAnimal(option)}
                />
              ))}
            </ScrollView>
          )
        ) : (
          <ScrollView contentContainerStyle={styles.bodyScroll}>
            <Card>
              <Body>{animal.label}</Body>

              {mode === 'treatment' ? (
                <View style={styles.listInner}>
                  {!medication ? (
                    medications.length === 0 ? (
                      <Body muted>
                        No hay medicamentos en el inventario. Agregue medicamentos desde el panel
                        y sincronice para poder registrar tratamientos.
                      </Body>
                    ) : (
                      medications.map((option) => (
                        <BigButton
                          key={option.itemId}
                          testID={`medication-${option.itemId}`}
                          label={option.name}
                          tone="neutral"
                          onPress={() => setMedication(option)}
                        />
                      ))
                    )
                  ) : (
                    <>
                      <Body muted>{medication.name}</Body>
                      {medication.milkWithdrawalDays ? (
                        <Notice
                          tone="warning"
                          text={`Al registrar, la leche queda no vendible por ${medication.milkWithdrawalDays} día(s).`}
                        />
                      ) : null}
                      <TextField label="Dosis" testID="dose-input" value={dose} onChangeText={setDose} />
                      <BigButton
                        testID="confirm-treatment"
                        label="Registrar tratamiento"
                        busy={busy}
                        onPress={() =>
                          run(
                            () =>
                              service.recordTreatment({
                                animalId: animal.animalId,
                                medicationId: medication.itemId,
                                medicationName: medication.name,
                                dose,
                                milkWithdrawalDays: medication.milkWithdrawalDays,
                                meatWithdrawalDays: medication.meatWithdrawalDays,
                              }),
                            'Tratamiento registrado.',
                          )
                        }
                      />
                    </>
                  )}
                </View>
              ) : null}

              {mode === 'weight' ? (
                <>
                  <NumberField label="Peso (kg)" testID="weight-input" value={weight} onChangeText={setWeight} />
                  <BigButton
                    testID="confirm-weight"
                    label="Registrar pesaje"
                    busy={busy}
                    onPress={() =>
                      run(
                        () =>
                          service.recordWeight({
                            animalId: animal.animalId,
                            weightKg: Number(weight.replace(',', '.')),
                          }),
                        'Pesaje registrado.',
                      )
                    }
                  />
                </>
              ) : null}

              {mode === 'move' ? (
                <View style={styles.listInner}>
                  {groups.length === 0 ? (
                    <Body muted>
                      No hay lotes configurados. Cree lotes desde el panel y sincronice para poder
                      registrar movimientos de lote.
                    </Body>
                  ) : (
                    groups.map((group) => (
                      <BigButton
                        key={group.groupId}
                        testID={`group-${group.groupId}`}
                        label={`Mover a ${group.label}`}
                        tone="neutral"
                        busy={busy}
                        onPress={() =>
                          run(
                            () =>
                              service.recordGroupMove({
                                animalId: animal.animalId,
                                toGroupId: group.groupId,
                              }),
                            'Movimiento registrado.',
                          )
                        }
                      />
                    ))
                  )}
                </View>
              ) : null}

              {mode === 'disposal' ? (
                <View style={styles.listInner}>
                  {animal.motherId ? (
                    <Body muted>
                      Madre: {animals.find((a) => a.animalId === animal.motherId)?.label ?? animal.motherId}
                    </Body>
                  ) : null}
                  {!cause ? (
                    !mortalityCauses || mortalityCauses.length === 0 ? (
                      <Body muted>
                        No hay causas de mortalidad configuradas. Agréguelas desde el panel y
                        sincronice para poder registrar la baja.
                      </Body>
                    ) : (
                      mortalityCauses.map((option) => (
                        <BigButton
                          key={option.causeId}
                          testID={`cause-${option.causeId}`}
                          label={option.name}
                          tone="neutral"
                          onPress={() => setCause(option)}
                        />
                      ))
                    )
                  ) : (
                    <>
                      <Body muted>{cause.name}</Body>
                      <BigButton
                        testID="confirm-disposal"
                        label="Registrar baja"
                        busy={busy}
                        onPress={() =>
                          run(
                            () =>
                              service.recordDisposal({
                                animalId: animal.animalId,
                                causeId: cause.causeId,
                              }),
                            'Baja registrada.',
                          )
                        }
                      />
                    </>
                  )}
                </View>
              ) : null}
            </Card>
          </ScrollView>
        )}
      </View>

      <BigButton testID="cancel-event" label="Volver" tone="neutral" onPress={reset} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  /**
   * Soaks up the empty middle so the picker (or "no animals") area takes the space the
   * user expects, instead of leaving a black void between the title and the Volver button.
   */
  body: {
    flex: 1,
  },
  bodyScroll: {
    flexGrow: 1,
  },
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
  listInner: {
    gap: theme.space.sm,
  },
});
