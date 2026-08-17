import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { theme } from '../../ui/theme';

const STEPS = ['Madre', 'Datos', 'Crías', 'Confirmar'];

export function StepIndicator({ currentStep }: { currentStep: number }) {
  return (
    <View testID="step-indicator" style={styles.container}>
      {STEPS.map((label, idx) => {
        const stepNum = idx + 1;
        const isActive = stepNum === currentStep;
        const isDone = stepNum < currentStep;

        return (
          <View key={stepNum} style={styles.stepItem}>
            <View
              testID={`step-dot-${stepNum}`}
              style={[
                styles.dot,
                isActive && styles.dotActive,
                isDone && styles.dotDone,
              ]}
            >
              <Text style={[styles.dotText, (isActive || isDone) && styles.dotTextActive]}>
                {stepNum}
              </Text>
            </View>
            <Text style={[styles.label, isActive && styles.labelActive]}>
              {label}
            </Text>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: theme.space.xs,
  },
  stepItem: {
    alignItems: 'center',
    flex: 1,
    gap: 4,
  },
  dot: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: theme.color.surfaceRaised,
    borderWidth: 1,
    borderColor: theme.color.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dotActive: {
    backgroundColor: theme.color.primary,
    borderColor: theme.color.primary,
  },
  dotDone: {
    backgroundColor: theme.color.surface,
    borderColor: theme.color.primary,
  },
  dotText: {
    fontSize: theme.font.label,
    fontWeight: '700',
    color: theme.color.textMuted,
  },
  dotTextActive: {
    color: theme.color.primaryText,
  },
  label: {
    fontSize: 11,
    color: theme.color.textMuted,
    fontWeight: '600',
  },
  labelActive: {
    color: theme.color.text,
    fontWeight: '700',
  },
});
