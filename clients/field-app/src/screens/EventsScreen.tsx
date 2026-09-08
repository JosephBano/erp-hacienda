import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Database } from '@nozbe/watermelondb';

import { theme } from '../ui/theme';
import {
  BigButton,
  Body,
  Card,
  EmptyState,
  Notice,
  NumberField,
  Screen,
  Title,
} from '../ui/components';
import type { EventService } from '../services/eventService';
import { evaluatePlausibility } from '../services/plausibilityService';
import { useSingleFlight } from '../ui/useSingleFlight';

export interface AnimalOption {
  animalId: string;
  label: string;
  /** Resolved automatically for "Baja con causa" (3.5a.3) while the animal is still its own row. */
  motherId?: string;
  /**
   * Needed to evaluate plausibility (ADR-0022) for the weight form. Optional
   * so screens that build a trimmed-down AnimalOption (e.g. BirthScreen's
   * dam/sire pickers) are unaffected — an undefined speciesId simply never
   * matches a range, which is the fail-open contract anyway.
   */
  speciesId?: string;
  categoryId?: string | null;
}

export interface GroupOption {
  groupId: string;
  label: string;
}

export interface MortalityCauseOption {
  causeId: string;
  name: string;
}

/**
 * The states the screen can be in. 'menu' is the picker of activity types; the
 * others are the dedicated forms. Pre-selection from the activity tree
 * (3.5a.9-B) lands directly in one of them, skipping the menu.
 *
 * Treatment (curative) and vaccination moved to their own screens
 * (`TreatScreen` / `VaccinateScreen`, 3.5a.2-C): the free-text `dose` field
 * this screen used to have here was the Art. 10 violation that sub-branch
 * exists to close, and mixing the "record a structured treatment" intention
 * into this generic picker was exactly what cost the three-taps rule for
 * vaccination.
 */
export type EventMode = 'menu' | 'weight' | 'move' | 'disposal';

/** Weighings, lot moves and individual disposals — recorded from the paddock. */
export function EventsScreen({
  service,
  database,
  animals,
  groups,
  mortalityCauses,
  onRecorded,
  initialAnimalId,
  initialActivity,
}: {
  service: EventService;
  /**
   * The local WatermelonDB handle, used to evaluate plausibility (ADR-0022)
   * against the mirrored `plausibility_ranges` table for the weight form.
   * Never used to reach the network — the check is 100% local (Art. 9).
   */
  database: Database;
  animals: AnimalOption[];
  groups: GroupOption[];
  mortalityCauses?: MortalityCauseOption[];
  onRecorded?: () => void;
  /**
   * Caller-supplied animal + activity to skip the picker steps. The activity tree
   * (3.5a.9-B) hands these in so the operator does not re-pick what they already
   * chose two screens ago. Undefined means: show the full menu (legacy path).
   */
  initialAnimalId?: string;
  initialActivity?: 'weight' | 'move' | 'disposal';
}) {
  const [mode, setMode] = useState<EventMode>('menu');
  const [animal, setAnimal] = useState<AnimalOption | null>(null);
  const [weight, setWeight] = useState('');
  const [cause, setCause] = useState<MortalityCauseOption | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);
  const { busy, runOnce } = useSingleFlight();
  /**
   * Set when `evaluatePlausibility` returns 'confirm' for the typed weight:
   * holds the number itself so the confirm button submits it directly,
   * without depending on `weight` still holding the same text (ADR-0022 sec.2).
   */
  const [weightPendingConfirmation, setWeightPendingConfirmation] = useState<number | null>(null);

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
    setWeight('');
    setCause(null);
    setError(null);
    setWeightPendingConfirmation(null);
  };

  /**
   * The shared body of every write: enqueue, confirm, go back to the menu.
   *
   * It deliberately does *not* take the single-flight latch itself. The latch
   * belongs at the press handler, because `recordWeight` awaits the
   * plausibility check *before* it gets here — and that await is precisely the
   * window a gloved double tap lands in (D2). Latching in both places would be
   * worse than latching in neither: the outer `runOnce` would refuse its own
   * inner one and the write would be dropped silently.
   */
  const run = async (action: () => Promise<unknown>, done: string) => {
    setError(null);
    try {
      await action();
      setConfirmation(done);
      reset();
      onRecorded?.();
    } catch (caught) {
      setError((caught as Error).message);
    }
  };

  /**
   * Plausibility gate for the weight form (ADR-0022, 3.5a.6). Runs entirely
   * offline against the local `plausibility_ranges` mirror: 'pass' (or no
   * range configured — fail-open) submits immediately, 'confirm' shows the
   * dialog below, 'block' stops the entry before it reaches the outbox.
   */
  const recordWeight = async () => {
    if (!animal) return;

    setError(null);
    const value = Number(weight.replace(',', '.'));

    if (Number.isFinite(value) && value > 0 && animal.speciesId) {
      const verdict = await evaluatePlausibility(database, {
        speciesId: animal.speciesId,
        categoryId: animal.categoryId ?? null,
        magnitude: 'weight_kg',
        value,
      });

      if (verdict === 'block') {
        setError(`${value} kg está fuera de lo posible para este animal. Verifica el dato.`);
        return;
      }
      if (verdict === 'confirm') {
        setWeightPendingConfirmation(value);
        return;
      }
    }

    await run(
      () => service.recordWeight({ animalId: animal.animalId, weightKg: value }),
      'Pesaje registrado.',
    );
  };

  if (mode === 'menu') {
    return (
      <Screen testID="events-screen">
        <Title>Registrar evento</Title>
        {confirmation ? <Body muted>{confirmation}</Body> : null}
        <BigButton testID="mode-weight" label="Pesaje" onPress={() => setMode('weight')} />
        <BigButton testID="mode-move" label="Cambio de lote" tone="neutral" onPress={() => setMode('move')} />
        <BigButton testID="mode-disposal" label="Baja con causa" tone="neutral" onPress={() => setMode('disposal')} />
      </Screen>
    );
  }

  const titleByMode: Record<Exclude<EventMode, 'menu'>, string> = {
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
          /*
           * The form keeps its own scroller instead of switching the whole `Screen` to
           * `scrollable`: the title above it and "Volver" below it are deliberately
           * pinned, and a screen-level scroller would carry them off with the content
           * — and nesting one around this scroller would give the drag two owners (D1).
           * What the scroller lacked was the keyboard contract that `Screen scrollable`
           * carries. React Native defaults `keyboardShouldPersistTaps` to 'never', so
           * with the keyboard open the first tap on "Registrar pesaje" is spent
           * dismissing it and the button never hears it: the "toco y no pasa nada" the
           * operators reported. 'on-drag' is the other half — dragging the form away
           * from the field puts the keyboard down and registers nothing (D2).
           */
          <ScrollView
            testID="events-form"
            contentContainerStyle={styles.bodyScroll}
            keyboardShouldPersistTaps="handled"
            keyboardDismissMode="on-drag"
          >
            <Card>
              <Body>{animal.label}</Body>

              {mode === 'weight' ? (
                <>
                  <NumberField label="Peso (kg)" testID="weight-input" value={weight} onChangeText={setWeight} />

                  {weightPendingConfirmation !== null ? (
                    <>
                      <Notice
                        tone="warning"
                        text={`${weightPendingConfirmation} kg es mucho más de lo normal para este animal. ¿Es correcto?`}
                      />
                      <BigButton
                        testID="weight-confirm-plausibility"
                        label="Sí, registrar"
                        busy={busy}
                        onPress={() =>
                          void runOnce(() =>
                            run(
                              () =>
                                service.recordWeight({
                                  animalId: animal.animalId,
                                  weightKg: weightPendingConfirmation,
                                  isPlausibilityConfirmed: true,
                                }),
                              'Pesaje registrado.',
                            ),
                          )
                        }
                      />
                      <BigButton
                        testID="weight-cancel-plausibility"
                        label="No, revisar"
                        tone="neutral"
                        onPress={() => setWeightPendingConfirmation(null)}
                      />
                    </>
                  ) : (
                    <BigButton
                      testID="confirm-weight"
                      label="Registrar pesaje"
                      busy={busy}
                      onPress={() => void runOnce(recordWeight)}
                    />
                  )}
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
                          void runOnce(() =>
                            run(
                              () =>
                                service.recordGroupMove({
                                  animalId: animal.animalId,
                                  toGroupId: group.groupId,
                                }),
                              'Movimiento registrado.',
                            ),
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
                          void runOnce(() =>
                            run(
                              () =>
                                service.recordDisposal({
                                  animalId: animal.animalId,
                                  causeId: cause.causeId,
                                }),
                              'Baja registrada.',
                            ),
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
