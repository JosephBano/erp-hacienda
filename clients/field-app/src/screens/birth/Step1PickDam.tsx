import React from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';

import { theme } from '../../ui/theme';
import { BigButton, Body, EmptyState } from '../../ui/components';
import type { PregnantDam } from '../../services/herdQueries';

export function Step1PickDam({
  dams,
  onSelectDam,
}: {
  dams: PregnantDam[];
  onSelectDam: (dam: PregnantDam) => void;
}) {
  return (
    <View style={styles.container}>
      <Body muted>¿Qué madre parió?</Body>

      {dams.length === 0 ? (
        <EmptyState
          testID="dam-list-empty"
          title="No hay hembras con preñez activa"
          hint="Solo aparecen hembras con preñez activa confirmada. La preñez se registra en el panel de administración."
        />
      ) : (
        <ScrollView testID="dam-list" contentContainerStyle={styles.list}>
          {dams.map((dam) => {
            const subtitle = dam.expectedBirthDate ? ` · FPP: ${dam.expectedBirthDate}` : '';
            return (
              <BigButton
                key={dam.animalId}
                testID={`dam-${dam.animalId}`}
                label={`${dam.label}${subtitle}`}
                tone="neutral"
                onPress={() => onSelectDam(dam)}
              />
            );
          })}
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    gap: theme.space.sm,
  },
  list: {
    gap: theme.space.sm,
    paddingBottom: theme.space.md,
  },
});
