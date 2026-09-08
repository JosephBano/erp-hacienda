import React, { useMemo, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

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
import { searchAnimals, type AnimalForSubject } from '../services/herdQueries';

export type AnimalActivity = 'treatment' | 'weight' | 'move' | 'disposal';

export type { AnimalForSubject };

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

function AnimalCardRow({
  animal,
  onPress,
  matchType,
  testID,
}: {
  animal: AnimalForSubject;
  onPress: () => void;
  matchType?: 'exact' | 'partial';
  testID?: string;
}) {
  const isUntagged = Boolean(
    animal.hasPendingTag ||
      (!animal.tag && (!animal.activeIdentifiers || animal.activeIdentifiers.length === 0) && (!animal.label || animal.label.includes('Sin arete') || !animal.tag))
  );

  const readableSnippet =
    animal.animalId && animal.animalId.length >= 6 ? animal.animalId.slice(-6) : undefined;

  const sexText = animal.sex
    ? animal.sex.toLowerCase() === 'female' || animal.sex.toLowerCase() === 'hembra'
      ? 'Hembra'
      : animal.sex.toLowerCase() === 'male' || animal.sex.toLowerCase() === 'macho'
        ? 'Macho'
        : animal.sex
    : null;

  const groupText = animal.groupName
    ? `Grupo: ${animal.groupName}`
    : animal.sex
      ? 'Sin grupo'
      : null;

  return (
    <Pressable
      testID={testID ?? `animal-row-${animal.animalId}`}
      accessibilityRole="button"
      accessibilityLabel={animal.label}
      onPress={onPress}
      style={({ pressed }) => [styles.animalCard, pressed && { opacity: 0.8 }]}
    >
      <View style={styles.cardHeaderRow}>
        {animal.tag ? (
          <View style={styles.tagBadge}>
            <Text style={styles.tagText}>Arete: {animal.tag}</Text>
          </View>
        ) : isUntagged ? (
          <View style={styles.pendingTagBadge} testID={`pending-tag-badge-${animal.animalId}`}>
            <Text style={styles.pendingTagText}>Sin arete</Text>
          </View>
        ) : null}

        {animal.matchedHistoricalTag ? (
          <View
            style={styles.historicalTagBadge}
            testID={`historical-tag-badge-${animal.animalId}`}
          >
            <Text style={styles.historicalTagText}>
              Arete anterior: {animal.matchedHistoricalTag}
            </Text>
          </View>
        ) : null}

        {matchType ? (
          <View style={matchType === 'exact' ? styles.exactBadge : styles.partialBadge}>
            <Text style={matchType === 'exact' ? styles.exactBadgeText : styles.partialBadgeText}>
              {matchType === 'exact' ? 'Coincidencia exacta' : 'Coincidencia parcial'}
            </Text>
          </View>
        ) : null}
      </View>

      <Text style={styles.animalTitle}>
        {animal.name && animal.name !== animal.tag ? `${animal.name} (${animal.label})` : animal.label}
        {animal.matchedHistoricalTag ? ` (Arete anterior: ${animal.matchedHistoricalTag})` : ''}
      </Text>

      {sexText || groupText || (isUntagged && readableSnippet) ? (
        <View style={styles.metaRow}>
          {sexText ? <Text style={styles.metaBadge}>{sexText}</Text> : null}
          {groupText ? <Text style={styles.metaBadge}>{groupText}</Text> : null}
          {isUntagged && readableSnippet ? (
            <Text style={styles.metaMuted}>ID: {readableSnippet}</Text>
          ) : null}
        </View>
      ) : null}
    </Pressable>
  );
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
    if (!query) {
      return recentIds
        .map((id) => animals.find((a) => a.animalId === id))
        .filter(Boolean) as AnimalForSubject[];
    }
    return [];
  }, [animals, query, recentIds]);

  const searchResult = useMemo(() => {
    return searchAnimals(animals, query);
  }, [animals, query]);

  const seen = useMemo(() => new Set(recent.map((a) => a.animalId)), [recent]);

  const matchesWhenEmpty = useMemo(() => {
    return animals.filter((a) => !seen.has(a.animalId));
  }, [animals, seen]);

  if (selectedAnimalId) {
    const animal = animals.find((a) => a.animalId === selectedAnimalId);
    if (!animal) {
      return (
        <Screen testID="animal-subject-detail">
          <Title>Animal no disponible</Title>
          <Notice
            tone="warning"
            text="El animal seleccionado ya no existe en el sistema (fue eliminado o dado de baja en el servidor)."
          />
          <BigButton testID="back-to-animal-picker" label="Elegir otro animal" tone="neutral" onPress={onClearSelection} />
        </Screen>
      );
    }

    return (
      <Screen testID="animal-subject-detail" scrollable>
        <Title>{animal.label}</Title>
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

  const isSearching = Boolean(query.trim());
  const exactMatches = searchResult.exactMatches;
  const partialMatches = searchResult.partialMatches;
  const hasMatches = exactMatches.length > 0 || partialMatches.length > 0;
  const isAmbiguous = exactMatches.length > 1;

  return (
    <Screen testID="animal-subject-screen" scrollable>
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
            <AnimalCardRow
              key={`recent-${animal.animalId}`}
              testID={`animal-row-${animal.animalId}`}
              animal={animal}
              onPress={() => onSelectAnimal(animal.animalId)}
            />
          ))}
        </Card>
      ) : null}

      <Card>
        <Body muted>{query ? `Coincidencias con "${query}"` : 'Todos los animales'}</Body>

        {!isSearching ? (
          matchesWhenEmpty.length === 0 ? (
            <Body muted>Nada coincide.</Body>
          ) : (
            <View testID="animal-list" style={styles.list}>
              {matchesWhenEmpty.map((animal) => (
                <AnimalCardRow
                  key={animal.animalId}
                  animal={animal}
                  onPress={() => onSelectAnimal(animal.animalId)}
                />
              ))}
            </View>
          )
        ) : !hasMatches ? (
          <Body muted>Nada coincide.</Body>
        ) : (
          <View testID="animal-list" style={styles.list}>
            {isAmbiguous ? (
              <Notice
                tone="warning"
                text={`Múltiples animales coinciden exactamente ("${query.trim()}"). Seleccione conscientemente el animal correspondiente.`}
              />
            ) : null}

            {exactMatches.length > 0 ? (
              <>
                <Body muted>{`Coincidencias exactas (${exactMatches.length})`}</Body>
                {exactMatches.map((animal) => (
                  <AnimalCardRow
                    key={animal.animalId}
                    animal={animal}
                    matchType="exact"
                    onPress={() => onSelectAnimal(animal.animalId)}
                  />
                ))}
              </>
            ) : null}

            {partialMatches.length > 0 ? (
              <>
                <Body muted>{`Coincidencias parciales (${partialMatches.length})`}</Body>
                {partialMatches.map((animal) => (
                  <AnimalCardRow
                    key={animal.animalId}
                    animal={animal}
                    matchType="partial"
                    onPress={() => onSelectAnimal(animal.animalId)}
                  />
                ))}
              </>
            ) : null}
          </View>
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
  animalCard: {
    minHeight: theme.touchTarget,
    backgroundColor: theme.color.surfaceRaised,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
    padding: theme.space.md,
    gap: theme.space.xs,
    justifyContent: 'center',
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: theme.space.xs,
  },
  tagBadge: {
    backgroundColor: theme.color.surface,
    paddingHorizontal: theme.space.sm,
    paddingVertical: 2,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.primary,
  },
  tagText: {
    color: theme.color.primary,
    fontSize: theme.font.body,
    fontWeight: '800',
  },
  pendingTagBadge: {
    backgroundColor: theme.color.warning,
    paddingHorizontal: theme.space.sm,
    paddingVertical: 2,
    borderRadius: theme.radius.md,
  },
  pendingTagText: {
    color: theme.color.warningText,
    fontSize: theme.font.label,
    fontWeight: '800',
  },
  historicalTagBadge: {
    backgroundColor: theme.color.surface,
    paddingHorizontal: theme.space.sm,
    paddingVertical: 2,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.warning,
  },
  historicalTagText: {
    color: theme.color.warning,
    fontSize: theme.font.label - 2,
    fontWeight: '800',
  },
  exactBadge: {
    backgroundColor: theme.color.primary,
    paddingHorizontal: theme.space.sm,
    paddingVertical: 2,
    borderRadius: theme.radius.md,
  },
  exactBadgeText: {
    color: theme.color.primaryText,
    fontSize: theme.font.label - 2,
    fontWeight: '700',
  },
  partialBadge: {
    backgroundColor: theme.color.surface,
    paddingHorizontal: theme.space.sm,
    paddingVertical: 2,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
  },
  partialBadgeText: {
    color: theme.color.textMuted,
    fontSize: theme.font.label - 2,
    fontWeight: '600',
  },
  animalTitle: {
    color: theme.color.text,
    fontSize: theme.font.body,
    fontWeight: '700',
  },
  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: theme.space.sm,
    marginTop: 2,
  },
  metaBadge: {
    color: theme.color.textMuted,
    fontSize: theme.font.label,
    fontWeight: '600',
  },
  metaMuted: {
    color: theme.color.textMuted,
    fontSize: theme.font.label - 2,
  },
});

