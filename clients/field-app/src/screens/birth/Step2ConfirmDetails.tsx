import React from 'react';
import { ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { theme } from '../../ui/theme';
import { BigButton, Body, Card } from '../../ui/components';
import type { PregnantDam } from '../../services/herdQueries';

export type BirthDifficulty = 'Normal' | 'Assisted' | 'Cesarean' | 'Dystocia';

const DIFFICULTIES: { key: BirthDifficulty; label: string }[] = [
  { key: 'Normal', label: 'Normal' },
  { key: 'Assisted', label: 'Asistido' },
  { key: 'Cesarean', label: 'Cesárea' },
  { key: 'Dystocia', label: 'Distocia' },
];

export function Step2ConfirmDetails({
  dam,
  birthDate,
  onChangeBirthDate,
  difficulty,
  onChangeDifficulty,
  onNext,
  onBack,
}: {
  dam: PregnantDam;
  birthDate: string;
  onChangeBirthDate: (date: string) => void;
  difficulty: BirthDifficulty;
  onChangeDifficulty: (difficulty: BirthDifficulty) => void;
  onNext: () => void;
  onBack: () => void;
}) {
  return (
    /*
     * This step owns the wizard's only vertical scroller — BirthScreen keeps its
     * `Screen` fixed so the four steps do not nest one inside another (D1). The date
     * field and "Continuar a Crías" share it, so `keyboardShouldPersistTaps` must be
     * 'handled': React Native's default ('never') spends the first tap on the button
     * dismissing the keyboard, and the operator reads that as the app ignoring them.
     */
    <ScrollView
      testID="birth-step-2"
      style={styles.container}
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
      keyboardDismissMode="on-drag"
    >
      <Card>
        <Body>{`Madre: ${dam.label}`}</Body>
        <Body muted>{dam.sireLabel}</Body>
      </Card>

      <View style={styles.field}>
        <Text style={styles.fieldLabel}>Fecha de parto (AAAA-MM-DD)</Text>
        <TextInput
          testID="birth-date-input"
          style={styles.input}
          value={birthDate}
          placeholder="AAAA-MM-DD"
          placeholderTextColor={theme.color.textMuted}
          onChangeText={onChangeBirthDate}
        />
      </View>

      <View style={styles.field}>
        <Text style={styles.fieldLabel}>Dificultad del parto</Text>
        <View style={styles.difficultyGrid}>
          {DIFFICULTIES.map((opt) => {
            const isSelected = opt.key === difficulty;
            return (
              <View key={opt.key} style={styles.difficultyItem}>
                <BigButton
                  testID={`difficulty-${opt.key.toLowerCase()}`}
                  label={opt.label}
                  tone={isSelected ? 'primary' : 'neutral'}
                  onPress={() => onChangeDifficulty(opt.key)}
                />
              </View>
            );
          })}
        </View>
      </View>

      <View style={styles.actions}>
        <BigButton testID="next-step-2" label="Continuar a Crías" onPress={onNext} />
        <BigButton testID="prev-step-2" label="Atrás" tone="neutral" onPress={onBack} />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  content: {
    gap: theme.space.md,
    paddingBottom: theme.space.md,
  },
  field: {
    gap: theme.space.xs,
  },
  fieldLabel: {
    color: theme.color.textMuted,
    fontSize: theme.font.label,
    fontWeight: '600',
  },
  input: {
    minHeight: theme.touchTarget,
    borderRadius: theme.radius.md,
    borderWidth: 2,
    borderColor: theme.color.border,
    backgroundColor: theme.color.surfaceRaised,
    color: theme.color.text,
    fontSize: theme.font.body,
    paddingHorizontal: theme.space.md,
  },
  difficultyGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: theme.space.xs,
  },
  difficultyItem: {
    flexBasis: '48%',
    flexGrow: 1,
  },
  actions: {
    gap: theme.space.sm,
    marginTop: theme.space.sm,
  },
});
