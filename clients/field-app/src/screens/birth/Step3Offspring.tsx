import React from 'react';
import { ScrollView, StyleSheet, TextInput, View } from 'react-native';

import { theme } from '../../ui/theme';
import { BigButton, Body, Card } from '../../ui/components';
import type { OffspringInput, Sex } from '../../services/birthService';

export function Step3Offspring({
  offspring,
  onAdd,
  onRemove,
  onToggleSex,
  onSetFarmTag,
  onSetWeight,
  onNext,
  onBack,
}: {
  offspring: OffspringInput[];
  onAdd: (sex: Sex) => void;
  onRemove: (index: number) => void;
  onToggleSex: (index: number) => void;
  onSetFarmTag: (index: number, tag: string) => void;
  onSetWeight: (index: number, weight: string) => void;
  onNext: () => void;
  onBack: () => void;
}) {
  const males = offspring.filter((c) => c.sex === 'M').length;
  const females = offspring.filter((c) => c.sex === 'F').length;

  return (
    <View style={styles.container}>
      <Card style={styles.headerCard}>
        <Body testID="offspring-count">{`Crías: ${offspring.length}`}</Body>
        {offspring.length > 0 ? (
          <Body testID="offspring-breakdown" muted>
            {`M: ${males} · F: ${females}`}
          </Body>
        ) : null}
      </Card>

      {/*
        Every calf row carries a tag field, a weight field and its two buttons, all
        inside this list. With the default `keyboardShouldPersistTaps` ('never') the tap
        that follows typing an arete — "Cambiar a Hembra", "Quitar" — is swallowed to
        dismiss the keyboard. The footer below stays outside the scroller on purpose:
        "Continuar a Resumen" is the step's last control and it must not scroll away.
      */}
      <ScrollView
        testID="offspring-list"
        style={styles.scrollList}
        contentContainerStyle={styles.listContent}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
      >
        {offspring.map((calf, index) => (
          <Card key={index} style={styles.offspringRow}>
            <Body muted>{`${index + 1}. ${calf.sex === 'M' ? 'Macho' : 'Hembra'}`}</Body>
            <TextInput
              testID={`offspring-tag-${index}`}
              style={styles.input}
              placeholder="Arete de la cría — opcional"
              placeholderTextColor={theme.color.textMuted}
              defaultValue={calf.farmTag ?? ''}
              onChangeText={(text) => onSetFarmTag(index, text)}
            />
            <TextInput
              testID={`offspring-weight-${index}`}
              style={styles.input}
              keyboardType="decimal-pad"
              placeholder="Peso al nacer (kg) — opcional"
              placeholderTextColor={theme.color.textMuted}
              defaultValue={calf.birthWeightKg?.toString() ?? ''}
              onChangeText={(text) => onSetWeight(index, text)}
            />
            <View style={styles.row}>
              <View style={styles.rowItem}>
                <BigButton
                  testID={`toggle-offspring-${index}`}
                  label={calf.sex === 'M' ? 'Cambiar a Hembra' : 'Cambiar a Macho'}
                  tone="neutral"
                  onPress={() => onToggleSex(index)}
                />
              </View>
              <View style={styles.rowItem}>
                <BigButton
                  testID={`remove-offspring-${index}`}
                  label="Quitar"
                  tone="danger"
                  onPress={() => onRemove(index)}
                />
              </View>
            </View>
          </Card>
        ))}
      </ScrollView>

      <View style={styles.footer}>
        <View style={styles.row}>
          <View style={styles.rowItem}>
            <BigButton testID="add-female" label="+ Hembra" onPress={() => onAdd('F')} />
          </View>
          <View style={styles.rowItem}>
            <BigButton testID="add-male" label="+ Macho" onPress={() => onAdd('M')} />
          </View>
        </View>
        <BigButton
          testID="next-step-3"
          label="Continuar a Resumen"
          disabled={offspring.length === 0}
          onPress={onNext}
        />
        <BigButton testID="prev-step-3" label="Atrás" tone="neutral" onPress={onBack} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    gap: theme.space.sm,
  },
  headerCard: {
    gap: theme.space.xs,
  },
  scrollList: {
    flex: 1,
  },
  listContent: {
    gap: theme.space.sm,
    paddingBottom: theme.space.sm,
  },
  offspringRow: {
    gap: theme.space.xs,
  },
  input: {
    borderWidth: 1,
    borderColor: theme.color.border,
    borderRadius: theme.radius.md,
    paddingHorizontal: theme.space.sm,
    paddingVertical: theme.space.xs,
    fontSize: theme.font.body,
    color: theme.color.text,
    backgroundColor: theme.color.surfaceRaised,
  },
  row: {
    flexDirection: 'row',
    gap: theme.space.sm,
  },
  rowItem: {
    flex: 1,
  },
  footer: {
    gap: theme.space.xs,
    paddingTop: theme.space.xs,
  },
});
