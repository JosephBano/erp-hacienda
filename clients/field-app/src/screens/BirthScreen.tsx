import React, { useState } from 'react';
import { ScrollView, StyleSheet, TextInput, View } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, EmptyState, Notice, Screen, Title } from '../ui/components';
import type { BirthService, OffspringInput, Sex } from '../services/birthService';
import type { AnimalOption } from './EventsScreen';

/**
 * Recording a birth in the paddock.
 *
 * The litter is built by tapping "macho"/"hembra" once per calf, so the same screen serves
 * a cow with one calf and a sow with nine — the count comes from the data, never from an
 * `if` on the species (Art. 8).
 */
export function BirthScreen({
  service,
  dams,
  sires,
  onRecorded,
}: {
  service: BirthService;
  dams: AnimalOption[];
  sires: AnimalOption[];
  onRecorded?: () => void;
}) {
  const [dam, setDam] = useState<AnimalOption | null>(null);
  const [sire, setSire] = useState<AnimalOption | null>(null);
  const [offspring, setOffspring] = useState<OffspringInput[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const addCalf = (sex: Sex) => setOffspring((current) => [...current, { sex }]);

  /**
   * PLAN-FASE-3-5-PORCINO 3.5a.0 #2: the litter is editable, not append-only. The client
   * reported "agregué cinco, la tercera era otra cosa y no la pude sacar" — once a calf
   * was added the screen had no way back. These two handlers keep the surviving order
   * intact because the payload that `recordBirth` receives is the same `offspring` array,
   * not a recomputed copy.
   */
  const removeCalf = (index: number) =>
    setOffspring((current) => current.filter((_, i) => i !== index));

  const toggleCalfSex = (index: number) =>
    setOffspring((current) =>
      current.map((calf, i) =>
        i === index ? { ...calf, sex: calf.sex === 'M' ? 'F' : 'M' } : calf,
      ),
    );

  const setCalfFarmTag = (index: number, rawText: string) => {
    const trimmed = rawText.trim();
    setOffspring((current) =>
      current.map((calf, i) =>
        i === index ? { ...calf, farmTag: trimmed || undefined } : calf,
      ),
    );
  };

  /**
   * PLAN-FASE-3-5-PORCINO 3.5a.4 task 3: birth weight is the first-day metric the client
   * uses to decide future mothers — a gilt of ≥1 kg is a promise, <0.7 kg usually is not.
   * The field-app lets the operator leave it blank (the litter is still valid), and the
   * input only accepts positive decimals so a typo doesn't silently land in the database.
   * Validation lives on submit, not on every keystroke, so the three-tap rule still holds.
   */
  const setCalfWeight = (index: number, rawText: string) => {
    const cleaned = rawText.replace(',', '.').trim();
    if (cleaned === '') {
      setOffspring((current) =>
        current.map((calf, i) => (i === index ? { ...calf, birthWeightKg: undefined } : calf)),
      );
      return;
    }

    const parsed = Number(cleaned);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('El peso al nacer debe ser un valor positivo (en kilogramos).');
      return;
    }

    setError(null);
    setOffspring((current) =>
      current.map((calf, i) => (i === index ? { ...calf, birthWeightKg: parsed } : calf)),
    );
  };

  const submit = async () => {
    if (!dam) return;

    setBusy(true);
    setError(null);
    try {
      await service.recordBirth({
        damId: dam.animalId,
        sireAnimalId: sire?.animalId,
        offspring,
      });
      setDam(null);
      setSire(null);
      setOffspring([]);
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="birth-screen">
      <Title>Parto</Title>

      {error ? <Notice text={error} /> : null}

      {!dam ? (
        <>
          <Body muted>¿Qué madre parió?</Body>
          <View style={styles.body}>
            {dams.length === 0 ? (
              <EmptyState
                testID="dam-list-empty"
                title="No hay hembras en el dispositivo"
                hint="Vaya a Inicio → Sincronización para descargar el hato antes de registrar un parto."
              />
            ) : (
              <ScrollView testID="dam-list" contentContainerStyle={styles.list}>
                {dams.map((option) => (
                  <BigButton
                    key={option.animalId}
                    testID={`dam-${option.animalId}`}
                    label={option.label}
                    tone="neutral"
                    onPress={() => setDam(option)}
                  />
                ))}
              </ScrollView>
            )}
          </View>
        </>
      ) : (
        <>
          <Card>
            <Body>{`Madre: ${dam.label}`}</Body>
            <Body muted>{sire ? `Padre: ${sire.label}` : 'Padre: sin registrar'}</Body>
            <Body testID="offspring-count">{`Crías: ${offspring.length}`}</Body>
            {offspring.length > 0 ? (
              <Body testID="offspring-breakdown" muted>
                {`M: ${offspring.filter((c) => c.sex === 'M').length} · F: ${offspring.filter((c) => c.sex === 'F').length}`}
              </Body>
            ) : null}
          </Card>

          <View style={styles.row}>
            <View style={styles.rowItem}>
              <BigButton testID="add-female" label="+ Hembra" onPress={() => addCalf('F')} />
            </View>
            <View style={styles.rowItem}>
              <BigButton testID="add-male" label="+ Macho" onPress={() => addCalf('M')} />
            </View>
          </View>

          {offspring.length > 0 ? (
            <ScrollView testID="offspring-list" contentContainerStyle={styles.offspringList}>
              {offspring.map((calf, index) => (
                <Card key={index} style={styles.offspringRow}>
                  <Body muted>
                    {`${index + 1}. ${calf.sex === 'M' ? 'Macho' : 'Hembra'}`}
                  </Body>
                  <TextInput
                    testID={`offspring-tag-${index}`}
                    style={styles.weightInput}
                    placeholder="Arete de la cría — opcional"
                    placeholderTextColor={theme.color.textMuted}
                    defaultValue={calf.farmTag ?? ''}
                    onChangeText={(text) => setCalfFarmTag(index, text)}
                  />
                  <TextInput
                    testID={`offspring-weight-${index}`}
                    style={styles.weightInput}
                    keyboardType="decimal-pad"
                    placeholder="Peso al nacer (kg) — opcional"
                    placeholderTextColor={theme.color.textMuted}
                    defaultValue={calf.birthWeightKg?.toString() ?? ''}
                    onChangeText={(text) => setCalfWeight(index, text)}
                  />
                  <View style={styles.row}>
                    <View style={styles.rowItem}>
                      <BigButton
                        testID={`toggle-offspring-${index}`}
                        label={calf.sex === 'M' ? 'Cambiar a Hembra' : 'Cambiar a Macho'}
                        tone="neutral"
                        onPress={() => toggleCalfSex(index)}
                      />
                    </View>
                    <View style={styles.rowItem}>
                      <BigButton
                        testID={`remove-offspring-${index}`}
                        label="Quitar"
                        tone="danger"
                        onPress={() => removeCalf(index)}
                      />
                    </View>
                  </View>
                </Card>
              ))}
            </ScrollView>
          ) : null}

          {sires.length > 0 && !sire ? (
            <ScrollView testID="sire-list" contentContainerStyle={styles.list}>
              {sires.map((option) => (
                <BigButton
                  key={option.animalId}
                  testID={`sire-${option.animalId}`}
                  label={`Padre: ${option.label}`}
                  tone="neutral"
                  onPress={() => setSire(option)}
                />
              ))}
            </ScrollView>
          ) : null}

          <BigButton
            testID="confirm-birth"
            label="Registrar parto"
            busy={busy}
            disabled={offspring.length === 0}
            onPress={submit}
          />
          <BigButton
            testID="cancel-birth"
            label="Cancelar"
            tone="neutral"
            onPress={() => {
              setDam(null);
              setSire(null);
              setOffspring([]);
              setError(null);
            }}
          />
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  /**
   * Dam picker must occupy the room between the prompt and the screen footer instead of
   * collapsing so that, with no animals on file, an empty state is the thing the
   * employee reads — not a black void under "¿Qué madre parió?".
   */
  body: {
    flex: 1,
  },
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
  row: {
    flexDirection: 'row',
    gap: theme.space.sm,
  },
  rowItem: {
    flex: 1,
  },
  offspringList: {
    gap: theme.space.sm,
  },
  offspringRow: {
    gap: theme.space.xs,
  },
  weightInput: {
    borderWidth: 1,
    borderColor: theme.color.border,
    borderRadius: theme.radius.md,
    paddingHorizontal: theme.space.sm,
    paddingVertical: theme.space.xs,
    fontSize: theme.font.body,
    color: theme.color.text,
    backgroundColor: theme.color.surface,
  },
});
