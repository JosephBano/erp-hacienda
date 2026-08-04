import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet } from 'react-native';

import { theme } from '../ui/theme';
import { BigButton, Body, Card, Notice, Screen, TextField, Title } from '../ui/components';
import type { AnimalEditService } from '../services/animalEditService';
import type { Database } from '@nozbe/watermelondb';
import { loadBreeds, loadCategories, type HerdMember } from '../services/herdQueries';

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
  const [busy, setBusy] = useState(false);
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

  const submit = async () => {
    if (!selected) return;

    setBusy(true);
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
    } finally {
      setBusy(false);
    }
  };

  if (!selected) {
    return (
      <Screen testID="animal-edit-screen">
        <Title>Editar animal</Title>
        {confirmation ? <Notice tone="warning" text={confirmation} /> : null}
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
      </Screen>
    );
  }

  return (
    <Screen testID="animal-edit-screen">
      <Title>Editar animal</Title>

      {error ? <Notice text={error} /> : null}

      <Card>
        <Body>{selected.label}</Body>

        <Body muted>Raza</Body>
        <ScrollView testID="edit-breed-list" contentContainerStyle={styles.list}>
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
        </ScrollView>

        <Body muted>Categoría</Body>
        <ScrollView testID="edit-category-list" contentContainerStyle={styles.list}>
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
        </ScrollView>

        <TextField label="Fecha de nacimiento (AAAA-MM-DD)" testID="edit-birthdate" value={birthDate} onChangeText={setBirthDate} />

        <BigButton testID="confirm-edit-animal" label="Guardar cambios" busy={busy} onPress={submit} />
        <BigButton testID="cancel-edit-animal" label="Cancelar" tone="neutral" onPress={reset} />
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingVertical: theme.space.xs,
  },
});
