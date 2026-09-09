import React, { useEffect, useMemo, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Q, type Database } from '@nozbe/watermelondb';

import { theme, useTheme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  Screen,
  TagBadge,
  TextField,
  Title,
} from '../ui/components';
import { RecordStatusBadge } from '../ui/recordStates';
import { searchAnimals, type AnimalForSubject } from '../services/herdQueries';
import type { Outbox, OutboxEntry } from '../services/outbox';
import type { AnimalEvent } from '../database/models';

export type AnimalActivity = 'treatment' | 'weight' | 'move' | 'disposal' | 'birth';

export type { AnimalForSubject };

export interface AnimalHistoryRecord {
  id: string;
  eventType: string;
  occurredAt: string;
  summary: string;
  details?: string;
  status?: 'confirmed' | 'pending' | 'rejected';
  clientOperationId?: string;
}

export interface AnimalSubjectScreenProps {
  animals: AnimalForSubject[];
  /**
   * IDs of the most recently acted-on animals on THIS phone. Surfaced first because
   * the common case is the animal you were just at. Empty for a fresh phone.
   */
  recentIds: string[];
  /** Optional — when set, the record & activities for that animal are rendered instead of the picker. */
  selectedAnimalId?: string;
  onSelectAnimal: (animalId: string) => void;
  onClearSelection: () => void;
  onActivity: (animalId: string, activity: AnimalActivity) => void;
  outbox?: Outbox;
  historyRecords?: AnimalHistoryRecord[];
  database?: Database;
  permissions?: string[];
  groups?: { groupId: string; label: string }[];
  initialQuery?: string;
  initialSex?: 'all' | 'female' | 'male';
  initialGroup?: string;
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

function formatOperationType(type: string, payload?: Record<string, unknown>): string {
  if (type === 'recordAnimalEvent' && payload) {
    if (payload.eventType === 'weighing' || payload.eventType === 'weight') {
      return 'Pesaje';
    }
    if (payload.eventType === 'disposal') {
      return 'Baja con causa';
    }
  }
  switch (type) {
    case 'recordTreatment':
    case 'treatment':
      return 'Tratamiento';
    case 'recordMilking':
    case 'milking':
      return 'Ordeño';
    case 'recordBirth':
    case 'birth':
      return 'Parto';
    case 'moveAnimal':
    case 'move':
      return 'Movimiento de lote';
    case 'disposal':
      return 'Baja con causa';
    case 'assignAnimalIdentifier':
      return 'Asignación de arete';
    case 'updateAnimal':
      return 'Edición de datos';
    case 'recordAnimalEvent':
      return 'Evento del animal';
    case 'weight':
      return 'Pesaje';
    default:
      return type;
  }
}

function formatOperationPayload(payload?: Record<string, unknown>): string | undefined {
  if (!payload) return undefined;
  const parts: string[] = [];

  if (payload.productName || payload.medicationName) {
    const name = String(payload.productName ?? payload.medicationName);
    const dose = payload.dose ? `${payload.dose} ${payload.unit ?? ''}`.trim() : '';
    parts.push(dose ? `${name} (${dose})` : name);
  } else if (payload.weightKg !== undefined || payload.weight !== undefined) {
    parts.push(`${payload.weightKg ?? payload.weight} kg`);
  } else if (payload.targetGroupName || payload.destinationGroupName) {
    parts.push(`Hacia: ${payload.targetGroupName ?? payload.destinationGroupName}`);
  } else if (payload.reason) {
    parts.push(String(payload.reason));
  } else if (payload.notes) {
    parts.push(String(payload.notes));
  }

  return parts.length > 0 ? parts.join(' · ') : undefined;
}

/**
 * The "Un animal" branch of the activity hub & animal record. Two states:
 *
 *   picker  — show filters (group, sex), recent first, search with leading zeros, full herd below.
 *   record  — once an animal is chosen, presents the canonical animal record (prominent tag badge,
 *             backed-only states for withdrawal/baja/pregnancy, segregated history without duplication,
 *             and apt/permitted actions).
 */
export function AnimalSubjectScreen({
  animals,
  recentIds,
  selectedAnimalId,
  onSelectAnimal,
  onClearSelection,
  onActivity,
  outbox,
  historyRecords,
  database,
  permissions,
  groups,
  initialQuery = '',
  initialSex = 'all',
  initialGroup = 'all',
}: AnimalSubjectScreenProps) {
  useTheme();
  const [query, setQuery] = useState(initialQuery);
  const [selectedSex, setSelectedSex] = useState<'all' | 'female' | 'male'>(initialSex);
  const [selectedGroup, setSelectedGroup] = useState<string>(initialGroup);

  const [outboxEntries, setOutboxEntries] = useState<OutboxEntry[]>([]);
  const [dbEvents, setDbEvents] = useState<AnimalEvent[]>([]);

  // Load outbox entries for the selected animal
  useEffect(() => {
    if (!outbox || !selectedAnimalId) {
      setOutboxEntries((prev) => (prev.length === 0 ? prev : []));
      return;
    }
    let active = true;
    outbox
      .all()
      .then((entries) => {
        if (!active) return;
        const relevant = entries.filter((e) => {
          const p = e.payload ?? {};
          return (
            p.animalId === selectedAnimalId ||
            p.damId === selectedAnimalId ||
            p.damAnimalId === selectedAnimalId
          );
        });
        setOutboxEntries(relevant);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [outbox, selectedAnimalId]);

  // Load database events for the selected animal
  useEffect(() => {
    if (!database || !selectedAnimalId) {
      setDbEvents((prev) => (prev.length === 0 ? prev : []));
      return;
    }
    let active = true;
    database
      .get<AnimalEvent>('animal_events')
      .query(Q.where('animal_id', selectedAnimalId))
      .fetch()
      .then((events) => {
        if (!active) return;
        setDbEvents(events.filter((e) => !e.isDeleted));
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [database, selectedAnimalId]);

  // Available groups for filtering
  const availableGroups = useMemo(() => {
    const set = new Set<string>();
    if (groups) {
      for (const g of groups) {
        if (g.label) set.add(g.label);
      }
    }
    for (const a of animals) {
      if (a.groupName) set.add(a.groupName);
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }, [groups, animals]);

  // Filter animals by sex and group
  const filteredHerd = useMemo(() => {
    return animals.filter((a) => {
      if (selectedSex === 'female') {
        const s = a.sex?.toLowerCase();
        if (s !== 'female' && s !== 'hembra') return false;
      } else if (selectedSex === 'male') {
        const s = a.sex?.toLowerCase();
        if (s !== 'male' && s !== 'macho') return false;
      }
      if (selectedGroup !== 'all') {
        if (a.groupName !== selectedGroup) return false;
      }
      return true;
    });
  }, [animals, selectedSex, selectedGroup]);

  // Recent animals matching active filters
  const recent = useMemo(() => {
    if (!query) {
      return recentIds
        .map((id) => filteredHerd.find((a) => a.animalId === id))
        .filter(Boolean) as AnimalForSubject[];
    }
    return [];
  }, [filteredHerd, query, recentIds]);

  // Search results within filtered herd (strictly preserves leading zeros)
  const searchResult = useMemo(() => {
    return searchAnimals(filteredHerd, query);
  }, [filteredHerd, query]);

  const seen = useMemo(() => new Set(recent.map((a) => a.animalId)), [recent]);

  const matchesWhenEmpty = useMemo(() => {
    return filteredHerd.filter((a) => !seen.has(a.animalId));
  }, [filteredHerd, seen]);

  // Confirmed history entries (T4.6, T4.7: deduplicated)
  const confirmedEvents = useMemo(() => {
    const result: Array<{
      id: string;
      eventType: string;
      occurredAt: string;
      summary: string;
      details?: string;
    }> = [];

    const seenIds = new Set<string>();

    // 1. From historyRecords prop
    if (historyRecords) {
      for (const rec of historyRecords) {
        if (rec.status === 'confirmed' || !rec.status) {
          if (!seenIds.has(rec.id)) {
            seenIds.add(rec.id);
            if (rec.clientOperationId) seenIds.add(rec.clientOperationId);
            result.push({
              id: rec.id,
              eventType: rec.eventType,
              occurredAt: rec.occurredAt,
              summary: rec.summary,
              details: rec.details,
            });
          }
        }
      }
    }

    // 2. From database animal_events
    for (const ev of dbEvents) {
      if (!seenIds.has(ev.id)) {
        seenIds.add(ev.id);
        result.push({
          id: ev.id,
          eventType: ev.eventType,
          occurredAt: ev.occurredAt,
          summary: formatOperationType(ev.eventType),
          details: ev.payloadJson ? formatOperationPayload(JSON.parse(ev.payloadJson)) : undefined,
        });
      }
    }

    // 3. From synced outbox entries
    for (const entry of outboxEntries) {
      if (entry.status === 'synced') {
        if (entry.resultRef && seenIds.has(entry.resultRef)) {
          continue;
        }
        if (seenIds.has(entry.clientOperationId)) {
          continue;
        }
        const id = entry.resultRef ?? entry.clientOperationId;
        seenIds.add(id);
        if (entry.resultRef) seenIds.add(entry.resultRef);
        seenIds.add(entry.clientOperationId);

        result.push({
          id,
          eventType: entry.operationType,
          occurredAt: entry.occurredAt,
          summary: formatOperationType(entry.operationType, entry.payload),
          details: formatOperationPayload(entry.payload),
        });
      }
    }

    return result.sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  }, [historyRecords, dbEvents, outboxEntries]);

  // Pending local history entries (T4.6, T4.7: deduplicated, absent once confirmed)
  const pendingEvents = useMemo(() => {
    const result: Array<{
      id: string;
      eventType: string;
      occurredAt: string;
      summary: string;
      details?: string;
    }> = [];

    const seenIds = new Set<string>();

    // From historyRecords prop
    if (historyRecords) {
      for (const rec of historyRecords) {
        if (rec.status === 'pending') {
          const isAlreadyConfirmed = confirmedEvents.some(
            (c) => c.id === rec.id || (rec.clientOperationId && c.id === rec.clientOperationId)
          );
          if (!isAlreadyConfirmed && !seenIds.has(rec.id)) {
            seenIds.add(rec.id);
            result.push({
              id: rec.id,
              eventType: rec.eventType,
              occurredAt: rec.occurredAt,
              summary: rec.summary,
              details: rec.details,
            });
          }
        }
      }
    }

    // From outbox entries with status === 'pending'
    for (const entry of outboxEntries) {
      if (entry.status === 'pending') {
        const isAlreadyConfirmed = confirmedEvents.some(
          (c) => c.id === entry.clientOperationId || (entry.resultRef && c.id === entry.resultRef)
        );
        if (!isAlreadyConfirmed && !seenIds.has(entry.clientOperationId)) {
          seenIds.add(entry.clientOperationId);
          result.push({
            id: entry.clientOperationId,
            eventType: entry.operationType,
            occurredAt: entry.occurredAt,
            summary: formatOperationType(entry.operationType, entry.payload),
            details: formatOperationPayload(entry.payload),
          });
        }
      }
    }

    return result.sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  }, [historyRecords, outboxEntries, confirmedEvents]);

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

    const isDisposed = Boolean(animal.disposedAt);
    const isFemale = animal.sex?.toLowerCase() === 'female' || animal.sex?.toLowerCase() === 'hembra';
    const readableSnippet =
      animal.animalId && animal.animalId.length >= 6 ? animal.animalId.slice(-6) : animal.animalId;
    const sexText = animal.sex
      ? isFemale
        ? 'Hembra'
        : animal.sex.toLowerCase() === 'male' || animal.sex.toLowerCase() === 'macho'
          ? 'Macho'
          : animal.sex
      : null;

    // Permissions (0008)
    const canWriteLivestock = !permissions || permissions.includes('livestock.animals.write') || permissions.includes('*');
    const canRecordBreeding = !permissions || permissions.includes('breeding.events.record') || permissions.includes('*');

    // Aptitude (0005)
    const canCalve = !isDisposed && isFemale && canRecordBreeding;

    return (
      <Screen testID="animal-subject-detail" scrollable>
        {/* Navigation back */}
        <BigButton
          testID="back-to-animal-picker"
          label="Elegir otro animal"
          tone="neutral"
          onPress={onClearSelection}
        />

        {/* Ficha Header & Tag (T4.4, T4.9) */}
        <Card>
          <Text style={styles.sectionOverline}>ARETE OFICIAL</Text>
          <View style={styles.tagBadgeLargeRow}>
            <TagBadge
              tag={animal.tag}
              label={animal.tag ? undefined : 'Sin arete'}
              size="large"
              tone={animal.tag ? 'default' : 'muted'}
              testID="animal-record-tag-badge"
            />
          </View>

          {animal.name && animal.name !== animal.tag ? (
            <Text testID="animal-record-name" style={styles.recordAnimalName}>
              Nombre: "{animal.name}"
            </Text>
          ) : null}

          <Text testID="animal-record-id" style={styles.recordAnimalId}>
            ID interno: {animal.animalId.length >= 8 ? animal.animalId.slice(-8) : readableSnippet}
          </Text>

          {/* Metadata Chips: Sexo, Grupo, Preñez (T4.4, T4.5) */}
          <View style={styles.metaRowChips}>
            {sexText ? (
              <View testID="animal-record-sex-badge" style={styles.recordChip}>
                <Text style={styles.recordChipText}>{sexText}</Text>
              </View>
            ) : null}
            {animal.groupName ? (
              <View testID="animal-record-group-badge" style={styles.recordChip}>
                <Text style={styles.recordChipText}>Grupo: {animal.groupName}</Text>
              </View>
            ) : null}
            {/* T4.5: Preñez SOLO cuando hay datos que la respaldan */}
            {animal.isPregnant || animal.expectedBirthDate ? (
              <View testID="animal-record-pregnancy-badge" style={[styles.recordChip, styles.recordChipHighlight]}>
                <Text style={styles.recordChipTextHighlight}>
                  Gestante{animal.expectedBirthDate ? `: FPP ${animal.expectedBirthDate}` : ''}
                </Text>
              </View>
            ) : null}
          </View>
        </Card>

        {/* Estados activos: Retiro, Baja (T4.5: NUNCA inventar "sano" o "disponible") */}
        {animal.isWithheld || animal.withheldUntil ? (
          <View testID="animal-record-withdrawal-notice" style={styles.withdrawalCard}>
            <Text style={styles.withdrawalTitle}>
              [!] PERÍODO DE RETIRO ACTIVO{animal.withheldUntil ? ` HASTA ${animal.withheldUntil}` : ''}
            </Text>
            <Text style={styles.withdrawalSubtitle}>
              Leche y carne no aptas para entrega ni consumo durante el retiro.
            </Text>
          </View>
        ) : null}

        {animal.disposedAt ? (
          <View testID="animal-record-disposed-notice" style={styles.disposedCard}>
            <Text style={styles.disposedTitle}>
              [!] DADO DE BAJA EL {animal.disposedAt.slice(0, 10)}
            </Text>
            <Text style={styles.disposedSubtitle}>
              Este animal ya no forma parte del hato activo.
            </Text>
          </View>
        ) : null}

        {/* Acciones sobre este animal (T4.10) */}
        <Card>
          <Title>Acciones sobre este animal</Title>
          {isDisposed ? (
            <Notice
              tone="warning"
              text="Animal dado de baja. No hay actividades disponibles."
            />
          ) : (
            <>
              {permissions && !canWriteLivestock && !canCalve ? (
                <Notice
                  tone="warning"
                  text="No tiene permisos para registrar actividades sobre este animal."
                />
              ) : null}

              {canCalve ? (
                <BigButton
                  testID="activity-birth"
                  label="Registrar parto"
                  onPress={() => onActivity(selectedAnimalId, 'birth')}
                />
              ) : null}

              {canWriteLivestock ? (
                <>
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
                </>
              ) : null}
            </>
          )}
        </Card>

        {/* Historial reciente segregado (T4.6, T4.7) */}
        <Card>
          <Title>Historial reciente</Title>

          {pendingEvents.length > 0 ? (
            <View testID="pending-history-section" style={styles.historySubSection}>
              <Body muted>Registros locales pendientes ({pendingEvents.length})</Body>
              {pendingEvents.map((item) => (
                <View
                  key={`pending-${item.id}`}
                  testID={`pending-history-item-${item.id}`}
                  style={styles.historyItemCard}
                >
                  <View style={styles.historyItemHeader}>
                    <Text style={styles.historyItemTitle}>
                      {item.occurredAt.slice(0, 16).replace('T', ' ')} · {item.summary}
                    </Text>
                    <RecordStatusBadge
                      status="local_pending"
                      label="● Pendiente"
                      size="normal"
                      testID={`status-badge-pending-${item.id}`}
                    />
                  </View>
                  {item.details ? (
                    <Text style={styles.historyItemDetails}>{item.details}</Text>
                  ) : null}
                </View>
              ))}
            </View>
          ) : null}

          {confirmedEvents.length > 0 ? (
            <View testID="confirmed-history-section" style={styles.historySubSection}>
              <Body muted>Hechos confirmados ({confirmedEvents.length})</Body>
              {confirmedEvents.map((item) => (
                <View
                  key={`confirmed-${item.id}`}
                  testID={`confirmed-history-item-${item.id}`}
                  style={styles.historyItemCard}
                >
                  <View style={styles.historyItemHeader}>
                    <Text style={styles.historyItemTitle}>
                      {item.occurredAt.slice(0, 16).replace('T', ' ')} · {item.summary}
                    </Text>
                    <RecordStatusBadge
                      status="synced"
                      label="✓ Sincronizado"
                      size="normal"
                      testID={`status-badge-confirmed-${item.id}`}
                    />
                  </View>
                  {item.details ? (
                    <Text style={styles.historyItemDetails}>{item.details}</Text>
                  ) : null}
                </View>
              ))}
            </View>
          ) : null}

          {pendingEvents.length === 0 && confirmedEvents.length === 0 ? (
            <Body muted testID="empty-history-text">
              No hay eventos registrados para este animal.
            </Body>
          ) : null}
        </Card>
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

      {/* T4.1 Filter Chips: Sexo */}
      <View style={styles.filterSection}>
        <Text style={styles.filterLabel}>Sexo:</Text>
        <View style={styles.chipRow}>
          <Pressable
            testID="filter-sex-all"
            accessibilityRole="button"
            accessibilityLabel="Filtrar por todos los sexos"
            onPress={() => setSelectedSex('all')}
            style={[styles.chip, selectedSex === 'all' && styles.chipActive]}
          >
            <Text style={[styles.chipText, selectedSex === 'all' && styles.chipTextActive]}>
              Todos
            </Text>
          </Pressable>
          <Pressable
            testID="filter-sex-female"
            accessibilityRole="button"
            accessibilityLabel="Filtrar solo hembras"
            onPress={() => setSelectedSex('female')}
            style={[styles.chip, selectedSex === 'female' && styles.chipActive]}
          >
            <Text style={[styles.chipText, selectedSex === 'female' && styles.chipTextActive]}>
              Hembras
            </Text>
          </Pressable>
          <Pressable
            testID="filter-sex-male"
            accessibilityRole="button"
            accessibilityLabel="Filtrar solo machos"
            onPress={() => setSelectedSex('male')}
            style={[styles.chip, selectedSex === 'male' && styles.chipActive]}
          >
            <Text style={[styles.chipText, selectedSex === 'male' && styles.chipTextActive]}>
              Machos
            </Text>
          </Pressable>
        </View>
      </View>

      {/* T4.1 Filter Chips: Grupo */}
      {availableGroups.length > 0 ? (
        <View style={styles.filterSection}>
          <Text style={styles.filterLabel}>Grupo:</Text>
          <View style={styles.chipWrapRow}>
            <Pressable
              testID="filter-group-all"
              accessibilityRole="button"
              accessibilityLabel="Filtrar por todos los grupos"
              onPress={() => setSelectedGroup('all')}
              style={[styles.chip, selectedGroup === 'all' && styles.chipActive]}
            >
              <Text style={[styles.chipText, selectedGroup === 'all' && styles.chipTextActive]}>
                Todos los grupos
              </Text>
            </Pressable>
            {availableGroups.map((group) => (
              <Pressable
                key={`group-filter-${group}`}
                testID={`filter-group-${group}`}
                accessibilityRole="button"
                accessibilityLabel={`Filtrar por grupo ${group}`}
                onPress={() => setSelectedGroup(group)}
                style={[styles.chip, selectedGroup === group && styles.chipActive]}
              >
                <Text style={[styles.chipText, selectedGroup === group && styles.chipTextActive]}>
                  {group}
                </Text>
              </Pressable>
            ))}
          </View>
        </View>
      ) : null}

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
  filterSection: {
    gap: theme.space.xs,
    marginBottom: theme.space.sm,
  },
  filterLabel: {
    color: theme.color.textMuted,
    fontSize: theme.font.label,
    fontWeight: '700',
  },
  chipRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: theme.space.xs,
  },
  chipWrapRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: theme.space.xs,
  },
  chip: {
    minHeight: 44,
    paddingHorizontal: theme.space.md,
    paddingVertical: theme.space.xs,
    borderRadius: theme.radius.md,
    backgroundColor: theme.color.surfaceRaised,
    borderWidth: 1,
    borderColor: theme.color.border,
    justifyContent: 'center',
    alignItems: 'center',
  },
  chipActive: {
    backgroundColor: theme.color.primary,
    borderColor: theme.color.primary,
  },
  chipText: {
    color: theme.color.text,
    fontSize: theme.font.body - 2,
    fontWeight: '600',
  },
  chipTextActive: {
    color: theme.color.primaryText,
    fontWeight: '700',
  },
  sectionOverline: {
    fontSize: theme.font.label - 2,
    fontWeight: '800',
    color: theme.color.textMuted,
    letterSpacing: 1,
    marginBottom: theme.space.xs,
  },
  tagBadgeLargeRow: {
    marginVertical: theme.space.xs,
    alignItems: 'flex-start',
  },
  recordAnimalName: {
    fontSize: theme.font.title - 4,
    fontWeight: '700',
    color: theme.color.text,
    marginTop: theme.space.xs,
  },
  recordAnimalId: {
    fontSize: theme.font.label,
    color: theme.color.textMuted,
    marginTop: 2,
  },
  metaRowChips: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: theme.space.xs,
    marginTop: theme.space.sm,
  },
  recordChip: {
    backgroundColor: theme.color.surfaceRaised,
    paddingHorizontal: theme.space.sm,
    paddingVertical: theme.space.xs,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
  },
  recordChipText: {
    fontSize: theme.font.label,
    color: theme.color.text,
    fontWeight: '600',
  },
  recordChipHighlight: {
    backgroundColor: theme.color.surface,
    borderColor: theme.color.primary,
  },
  recordChipTextHighlight: {
    fontSize: theme.font.label,
    color: theme.color.primary,
    fontWeight: '700',
  },
  withdrawalCard: {
    backgroundColor: theme.color.warning,
    borderRadius: theme.radius.md,
    padding: theme.space.md,
    gap: theme.space.xs,
  },
  withdrawalTitle: {
    fontSize: theme.font.body,
    fontWeight: '800',
    color: theme.color.warningText,
  },
  withdrawalSubtitle: {
    fontSize: theme.font.label,
    color: theme.color.warningText,
    fontWeight: '500',
  },
  disposedCard: {
    backgroundColor: theme.color.danger,
    borderRadius: theme.radius.md,
    padding: theme.space.md,
    gap: theme.space.xs,
  },
  disposedTitle: {
    fontSize: theme.font.body,
    fontWeight: '800',
    color: theme.color.dangerText,
  },
  disposedSubtitle: {
    fontSize: theme.font.label,
    color: theme.color.dangerText,
    fontWeight: '500',
  },
  historySubSection: {
    gap: theme.space.xs,
    marginTop: theme.space.sm,
  },
  historyItemCard: {
    backgroundColor: theme.color.surfaceRaised,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
    padding: theme.space.sm,
    gap: 4,
  },
  historyItemHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: theme.space.xs,
  },
  historyItemTitle: {
    fontSize: theme.font.label,
    fontWeight: '700',
    color: theme.color.text,
    flex: 1,
  },
  historyItemDetails: {
    fontSize: theme.font.label - 2,
    color: theme.color.textMuted,
  },
});
