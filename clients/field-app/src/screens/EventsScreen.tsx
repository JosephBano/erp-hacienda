import React, { useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, Notice, NumberField, Screen, TextField, Title } from '../ui/components';
import type { EventService } from '../services/eventService';

export interface AnimalOption {
  animalId: string;
  label: string;
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

type Mode = 'menu' | 'treatment' | 'weight' | 'move';

/** Treatments, weighings and lot moves — the three events recorded from the paddock. */
export function EventsScreen({
  service,
  animals,
  groups,
  medications,
  onRecorded,
}: {
  service: EventService;
  animals: AnimalOption[];
  groups: GroupOption[];
  medications: MedicationOption[];
  onRecorded?: () => void;
}) {
  const [mode, setMode] = useState<Mode>('menu');
  const [animal, setAnimal] = useState<AnimalOption | null>(null);
  const [medication, setMedication] = useState<MedicationOption | null>(null);
  const [dose, setDose] = useState('');
  const [weight, setWeight] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const reset = () => {
    setMode('menu');
    setAnimal(null);
    setMedication(null);
    setDose('');
    setWeight('');
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
      </Screen>
    );
  }

  return (
    <Screen testID="events-screen">
      <Title>
        {mode === 'treatment' ? 'Tratamiento' : mode === 'weight' ? 'Pesaje' : 'Cambio de lote'}
      </Title>

      {error ? <Notice text={error} /> : null}

      {!animal ? (
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
      ) : (
        <Card>
          <Body>{animal.label}</Body>

          {mode === 'treatment' ? (
            <View style={styles.list}>
              {!medication ? (
                medications.map((option) => (
                  <BigButton
                    key={option.itemId}
                    testID={`medication-${option.itemId}`}
                    label={option.name}
                    tone="neutral"
                    onPress={() => setMedication(option)}
                  />
                ))
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
            <View style={styles.list}>
              {groups.map((group) => (
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
              ))}
            </View>
          ) : null}
        </Card>
      )}

      <BigButton testID="cancel-event" label="Volver" tone="neutral" onPress={reset} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
});
