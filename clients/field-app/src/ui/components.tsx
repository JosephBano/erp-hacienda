import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  ViewStyle,
} from 'react-native';

import { theme } from './theme';

type ButtonTone = 'primary' | 'neutral' | 'danger';

export function BigButton({
  label,
  onPress,
  tone = 'primary',
  disabled,
  busy,
  testID,
  hint,
}: {
  label: string;
  onPress: () => void;
  tone?: ButtonTone;
  disabled?: boolean;
  busy?: boolean;
  testID?: string;
  hint?: string;
}) {
  const background =
    tone === 'primary'
      ? theme.color.primary
      : tone === 'danger'
        ? theme.color.danger
        : theme.color.surfaceRaised;

  const color = tone === 'primary' ? theme.color.primaryText : theme.color.text;

  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={hint}
      accessibilityState={{ disabled: Boolean(disabled || busy) }}
      disabled={disabled || busy}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        { backgroundColor: background, opacity: disabled ? 0.45 : pressed ? 0.8 : 1 },
      ]}
    >
      {busy ? <ActivityIndicator color={color} /> : <Text style={[styles.buttonLabel, { color }]}>{label}</Text>}
    </Pressable>
  );
}

export function Screen({ children, testID }: { children: React.ReactNode; testID?: string }) {
  return (
    <View testID={testID} style={styles.screen}>
      {children}
    </View>
  );
}

export function Title({ children }: { children: React.ReactNode }) {
  return <Text style={styles.title}>{children}</Text>;
}

export function Body({
  children,
  muted,
  testID,
}: {
  children: React.ReactNode;
  muted?: boolean;
  testID?: string;
}) {
  return (
    <Text testID={testID} style={[styles.body, muted && { color: theme.color.textMuted }]}>
      {children}
    </Text>
  );
}

export function Card({ children, style }: { children: React.ReactNode; style?: ViewStyle }) {
  return <View style={[styles.card, style]}>{children}</View>;
}

/**
 * A message the employee must be able to act on. Errors are never swallowed into a
 * console log: the person in the paddock is the only one who can fix most of them.
 */
export function Notice({ text, tone = 'danger' }: { text: string; tone?: 'danger' | 'warning' }) {
  const background = tone === 'danger' ? theme.color.danger : theme.color.warning;
  const color = tone === 'danger' ? theme.color.text : theme.color.warningText;

  return (
    <View accessibilityRole="alert" style={[styles.notice, { backgroundColor: background }]}>
      <Text style={[styles.noticeText, { color }]}>{text}</Text>
    </View>
  );
}

export function NumberField({
  label,
  value,
  onChangeText,
  testID,
  placeholder,
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  testID?: string;
  placeholder?: string;
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        keyboardType="decimal-pad"
        placeholder={placeholder}
        placeholderTextColor={theme.color.textMuted}
        style={styles.input}
      />
    </View>
  );
}

export function TextField({
  label,
  value,
  onChangeText,
  testID,
  secure,
  autoCapitalize = 'none',
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  testID?: string;
  secure?: boolean;
  autoCapitalize?: 'none' | 'sentences';
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        secureTextEntry={secure}
        autoCapitalize={autoCapitalize}
        style={styles.input}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: theme.color.background,
    padding: theme.space.md,
    gap: theme.space.md,
  },
  button: {
    minHeight: theme.touchTarget,
    borderRadius: theme.radius.lg,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: theme.space.lg,
  },
  buttonLabel: {
    fontSize: theme.font.body,
    fontWeight: '800',
    textAlign: 'center',
  },
  title: {
    color: theme.color.text,
    fontSize: theme.font.title,
    fontWeight: '800',
  },
  body: {
    color: theme.color.text,
    fontSize: theme.font.body,
  },
  card: {
    backgroundColor: theme.color.surface,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
    padding: theme.space.md,
    gap: theme.space.sm,
  },
  notice: {
    borderRadius: theme.radius.md,
    padding: theme.space.md,
  },
  noticeText: {
    fontSize: theme.font.label,
    fontWeight: '700',
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
});
