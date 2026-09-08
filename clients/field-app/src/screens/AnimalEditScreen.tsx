import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';

import { theme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  Screen,
  TextField,
  Title,
} from '../ui/components';
import type { AnimalEditService } from '../services/animalEditService';
import type { Database } from '@nozbe/watermelondb';
import { loadBreeds, loadCategories, type HerdMember } from '../services/herdQueries';
import { useSingleFlight } from '../ui/useSingleFlight';

/**
 * The one screen that can produce an LWW conflict (ADR-0008): two employees editing the
 * same animal's breed or category from two phones, offline, at the same time. Everything
 * else in the app is an event — a new row that can never collide with another device's
 * new row.
 *
 * On purpose, this screen does not claim the edit already happened: it queues the
 * operation and says so plainly. Whether this device's edit wins the LWW race is decided
 * by the server, and showing it as applied before that answer arrives would sometimes be
 * showing a value the resolution never produced.
 */
export function AnimalEditScreen({
  database,
  service,
  animals,
  onQueued,
}: {
  database: Database;
  service: AnimalEditService;
  animals: HerdMember[];
  onQueued?: () => void;
}) {
  const [selected, setSelected] = useState<HerdMember | null>(null);
  const [breeds, setBreeds] = useState<{ breedId: string; label: string }[]>([]);
  const [categories, setCategories] = useState<{ categoryId: string; label: string }[]>([]);
  const [breedId, setBreedId] = useState<string>('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [birthDate, setBirthDate] = useState('');
  const { busy, runOnce } = useSingleFlight();
  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);

  useEffect(() => {
    if (!selected) return;

    setBreedId(selected.breedId ?? '');
    setCategoryId(selected.categoryId ?? '');
    setBirthDate(selected.birthDate ?? '');

    loadBreeds(database, selected.speciesId).then(setBreeds);
    loadCategories(database, selected.speciesId).then(setCategories);
  }, [selected, database]);

  const reset = () => {
    setSelected(null);
    setBreeds([]);
    setCategories([]);
    setError(null);
  };

  /*
   * Two taps on "Guardar cambios" used to queue two edit operations. Here that
   * costs more than a duplicate: each edit is an LWW write (ADR-0008), so the
   * second one races the first through the same resolution and the employee
   * gets two answers about one change. `runOnce` refuses the second tap in the
   * tap's own tick, before `editAnimal` has yielded (D2).
   */
  const submit = () =>
    runOnce(async () => {
      if (!selected) return;

      setError(null);

      try {
        await service.editAnimal({
          animalId: selected.animalId,
          breedId: breedId || null,
          categoryId: categoryId || null,
          birthDate: birthDate || null,
        });

        setConfirmation(
          `Cambio de "${selected.label}" en cola. Se verá reflejado al sincronizar — otro dispositivo pudo haber editado lo mismo mientras tanto.`,
        );
        reset();
        onQueued?.();
      } catch (caught) {
        setError((caught as Error).message);
      }
    });

  if (!selected) {
    return (
      <Screen testID="animal-edit-screen">
        <Title>Editar animal</Title>
        {confirmation ? <Notice tone="warning" text={confirmation} /> : null}
        <View style={styles.body}>
          {animals.length === 0 ? (
            <EmptyState
              testID="edit-animal-empty"
              title="No hay animales en el dispositivo"
              hint="Vaya a Inicio → Sincronización para descargar el hato antes de editar animales."
            />
          ) : (
            <ScrollView testID="edit-animal-list" contentContainerStyle={styles.list}>
              {animals.map((animal) => (
                <BigButton
                  key={animal.animalId}
                  testID={`edit-animal-${animal.animalId}`}
                  label={animal.label}
                  tone="neutral"
                  onPress={() => {
                    setSelected(animal);
                    setConfirmation(null);
                  }}
                />
              ))}
            </ScrollView>
          )}
        </View>
      </Screen>
    );
  }

  return (
    <Screen testID="animal-edit-screen">
      <Title>Editar animal</Title>

      {error ? <Notice text={error} /> : null}

      <View style={styles.body}>
        {/*
          The card scrolls, the screen does not: `Screen scrollable` around this scroller
          would put two owners on the same vertical drag (D1), and the title is meant to
          stay put. The keyboard settings are the part that was missing. React Native
          defaults `keyboardShouldPersistTaps` to 'never', so once the birth-date field
          has focus the first tap on "Guardar cambios" only dismisses the keyboard —
          the employee taps, nothing happens, they tap again. 'on-drag' lets a drag put
          the keyboard down without registering anything (D2).
        */}
        <ScrollView
          testID="animal-edit-form"
          contentContainerStyle={styles.bodyScroll}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode="on-drag"
        >
          <Card>
            <Body>{selected.label}</Body>

            <Body muted>Raza</Body>
            <View style={styles.list}>
              <BigButton
                testID="edit-breed-none"
                label="— Sin especificar —"
                tone={breedId === '' ? 'primary' : 'neutral'}
                onPress={() => setBreedId('')}
              />
              {breeds.map((breed) => (
                <BigButton
                  key={breed.breedId}
                  testID={`edit-breed-${breed.breedId}`}
                  label={breed.label}
                  tone={breedId === breed.breedId ? 'primary' : 'neutral'}
                  onPress={() => setBreedId(breed.breedId)}
                />
              ))}
            </View>

            <Body muted>Categoría</Body>
            <View style={styles.list}>
              <BigButton
                testID="edit-category-none"
                label="— Sin especificar —"
                tone={categoryId === '' ? 'primary' : 'neutral'}
                onPress={() => setCategoryId('')}
              />
              {categories.map((category) => (
                <BigButton
                  key={category.categoryId}
                  testID={`edit-category-${category.categoryId}`}
                  label={category.label}
                  tone={categoryId === category.categoryId ? 'primary' : 'neutral'}
                  onPress={() => setCategoryId(category.categoryId)}
                />
              ))}
            </View>

            <TextField
              label="Fecha de nacimiento (AAAA-MM-DD)"
              testID="edit-birthdate"
              value={birthDate}
              onChangeText={setBirthDate}
            />

            <BigButton
              testID="confirm-edit-animal"
              label="Guardar cambios"
              busy={busy}
              onPress={() => void submit()}
            />
            <BigButton testID="cancel-edit-animal" label="Cancelar" tone="neutral" onPress={reset} />
          </Card>
        </ScrollView>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  /**
   * The edit card is taller than most screens — the breed/category pickers alone are
   * three rows of 64pt each. Without flex:1 here, the card can render off-screen with no
   * obvious way back. Together with the ScrollView it always lands the confirm button
   * somewhere reachable with a swipe.
   */
  body: {
    flex: 1,
  },
  bodyScroll: {
    flexGrow: 1,
  },
  list: {
    gap: theme.space.sm,
    paddingVertical: theme.space.xs,
  },
});
