import React, { useMemo, useState } from 'react';
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

export type AnimalActivity = 'treatment' | 'weight' | 'move' | 'disposal';

export interface AnimalForSubject {
  animalId: string;
  label: string;
}

interface AnimalSubjectScreenProps {
  animals: AnimalForSubject[];
  /**
   * IDs of the most recently acted-on animals on THIS phone. Surfaced first because
   * the common case is the animal you were just at. Empty for a fresh phone.
   */
  recentIds: string[];
  /** Optional — when set, the activities for that animal are rendered instead of the picker. */
  selectedAnimalId?: string;
  onSelectAnimal: (animalId: string) => void;
  onClearSelection: () => void;
  onActivity: (animalId: string, activity: AnimalActivity) => void;
}

/**
 * The "Un animal" branch of the activity hub. Two states:
 *
 *   picker  — show recent first, full herd below, search on top.
 *   detail  — once an animal is chosen, list the activities for that one animal.
 *
 * Reachable activities:
 *  - treatment, weight, move → EventsScreen (existing flow).
 *  - disposal                  → routes to EventsScreen like the rest, now that the
 *                                mortality causes catalogue (3.5a.3) exists.
 */
export function AnimalSubjectScreen({
  animals,
  recentIds,
  selectedAnimalId,
  onSelectAnimal,
  onClearSelection,
  onActivity,
}: AnimalSubjectScreenProps) {
  const [query, setQuery] = useState('');

  const recent = useMemo(() => {
    if (!query) return recentIds.map((id) => animals.find((a) => a.animalId === id)).filter(Boolean) as AnimalForSubject[];
    return [];
  }, [animals, query, recentIds]);

  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const seen = new Set(recent.map((a) => a.animalId));
    const filtered = needle
      ? animals.filter((a) => a.label.toLowerCase().includes(needle))
      : animals;
    return filtered.filter((a) => !seen.has(a.animalId));
  }, [animals, query, recent]);

  if (selectedAnimalId) {
    const animal = animals.find((a) => a.animalId === selectedAnimalId);
    return (
      <Screen testID="animal-subject-detail">
        <Title>{animal?.label ?? selectedAnimalId}</Title>
        <BigButton
          testID="activity-treatment"
          label="Tratamiento (animal enfermo)"
          onPress={() => onActivity(selectedAnimalId, 'treatment')}
        />
        <BigButton
          testID="activity-weight"
          label="Pesaje"
          tone="neutral"
          onPress={() => onActivity(selectedAnimalId, 'weight')}
        />
        <BigButton
          testID="activity-move"
          label="Mover de lote"
          tone="neutral"
          onPress={() => onActivity(selectedAnimalId, 'move')}
        />
        <BigButton
          testID="activity-disposal"
          label="Baja con causa"
          tone="neutral"
          onPress={() => onActivity(selectedAnimalId, 'disposal')}
        />
        <BigButton testID="back-to-animal-picker" label="Elegir otro animal" tone="neutral" onPress={onClearSelection} />
      </Screen>
    );
  }

  if (animals.length === 0) {
    return (
      <Screen testID="animal-subject-screen">
        <Title>Un animal</Title>
        <EmptyState
          testID="animal-subject-empty"
          title="No hay animales en el dispositivo"
          hint="Vaya a Inicio → Sincronización para descargar el hato."
        />
      </Screen>
    );
  }

  return (
    <Screen testID="animal-subject-screen">
      <Title>Un animal</Title>
      <TextField
        testID="animal-subject-search"
        label="Buscar"
        value={query}
        onChangeText={setQuery}
      />

      {recent.length > 0 ? (
        <Card>
          <Body muted>{'Recientes'}</Body>
          {recent.map((animal) => (
            <BigButton
              key={`recent-${animal.animalId}`}
              testID={`animal-row-${animal.animalId}`}
              label={animal.label}
              tone="neutral"
              onPress={() => onSelectAnimal(animal.animalId)}
            />
          ))}
        </Card>
      ) : null}

      <Card>
        <Body muted>{query ? `Coincidencias con "${query}"` : 'Todos los animales'}</Body>
        {matches.length === 0 ? (
          <Body muted>Nada coincide.</Body>
        ) : (
          <ScrollView testID="animal-list" contentContainerStyle={styles.list}>
            {matches.map((animal) => (
              <BigButton
                key={animal.animalId}
                testID={`animal-row-${animal.animalId}`}
                label={animal.label}
                tone="neutral"
                onPress={() => onSelectAnimal(animal.animalId)}
              />
            ))}
          </ScrollView>
        )}
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
});
