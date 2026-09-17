import React from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';

import { theme } from '../../ui/theme';
import { BigButton, Body, Card, Notice } from '../../ui/components';
import type { PregnantDam } from '../../services/herdQueries';
import type { OffspringInput } from '../../services/birthService';
import type { BirthDifficulty } from './Step2ConfirmDetails';

const DIFFICULTY_LABELS: Record<BirthDifficulty, string> = {
  Normal: 'Normal',
  Assisted: 'Asistido',
  Cesarean: 'Cesárea',
  Dystocia: 'Distocia',
};

export function Step4Review({
  dam,
  birthDate,
  difficulty,
  offspring,
  busy,
  isDamObsolete,
  onChangeDam,
  onSubmit,
  onBack,
  onCancel,
}: {
  dam: PregnantDam;
  birthDate: string;
  difficulty: BirthDifficulty;
  offspring: OffspringInput[];
  busy: boolean;
  isDamObsolete?: boolean;
  onChangeDam?: () => void;
  onSubmit: () => void;
  onBack: () => void;
  onCancel: () => void;
}) {
  const males = offspring.filter((c) => c.sex === 'M').length;
  const females = offspring.filter((c) => c.sex === 'F').length;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Card>
        <Body>{`Madre: ${dam.label}`}</Body>

        {isDamObsolete ? (
          <>
            <Notice
              tone="warning"
              text="La madre seleccionada o su preñez ya no existe en el sistema (fue eliminada, dada de baja o completada en el servidor). No se puede registrar el parto contra esta madre. Puede elegir otra madre sin perder los datos ingresados."
            />
            <BigButton
              testID="change-dam"
              label="Elegir otra madre"
              tone="neutral"
              onPress={onChangeDam}
            />
          </>
        ) : null}

        <Body muted>{dam.sireLabel}</Body>
        <Body muted>{`Fecha: ${birthDate}`}</Body>
        <Body muted>{`Dificultad: ${DIFFICULTY_LABELS[difficulty]}`}</Body>
        <Body testID="offspring-count">{`Crías: ${offspring.length}`}</Body>
        {offspring.length > 0 ? (
          <Body testID="offspring-breakdown" muted>
            {`M: ${males} · F: ${females}`}
          </Body>
        ) : null}
      </Card>

      <View style={styles.actions}>
        <BigButton
          testID="confirm-birth"
          label="Registrar parto"
          busy={busy}
          disabled={offspring.length === 0 || isDamObsolete}
          onPress={onSubmit}
        />
        <BigButton
          testID="prev-step-4"
          label="Atrás"
          tone="neutral"
          disabled={busy}
          onPress={onBack}
        />
        <BigButton
          testID="cancel-birth"
          label="Cancelar"
          tone="neutral"
          disabled={busy}
          onPress={onCancel}
        />
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
  actions: {
    gap: theme.space.sm,
    marginTop: theme.space.sm,
  },
});
