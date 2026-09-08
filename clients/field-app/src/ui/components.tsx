import React from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  ViewStyle,
} from 'react-native';

import { theme, useTheme } from './theme';

type ButtonTone = 'primary' | 'neutral' | 'danger' | 'warning';

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
  /**
   * Optional when the button is disabled/busy — disabled surfaces (e.g. a non-yet-shipped
   * feature stub) cannot be tapped, so onPress would be dead code if required.
   */
  onPress?: () => void;
  tone?: ButtonTone;
  disabled?: boolean;
  busy?: boolean;
  testID?: string;
  hint?: string;
}) {
  const { theme: activeTheme } = useTheme();

  const background =
    tone === 'primary'
      ? activeTheme.color.primary
      : tone === 'danger'
        ? activeTheme.color.danger
        : tone === 'warning'
          ? activeTheme.color.warning
          : activeTheme.color.surfaceRaised;

  const color =
    tone === 'primary'
      ? activeTheme.color.primaryText
      : tone === 'warning'
        ? activeTheme.color.warningText
        : tone === 'danger'
          ? activeTheme.color.dangerText
          : activeTheme.color.text;

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
        {
          minHeight: activeTheme.touchTarget,
          backgroundColor: background,
          opacity: disabled ? 0.45 : pressed ? 0.8 : 1,
        },
      ]}
    >
      {busy ? (
        <ActivityIndicator color={color} />
      ) : (
        <Text style={[styles.buttonLabel, { color, fontSize: activeTheme.font.body }]}>{label}</Text>
      )}
    </Pressable>
  );
}

/**
 * The container every screen sits in.
 *
 * `scrollable` is opt-in on purpose: a screen that owns a bounded inner list
 * (MilkingScreen's picker) must not nest a second vertical scroller inside this
 * one, or the two compete for the same drag and the inner one traps it
 * (feature-0006 D1). One vertical gesture per screen, always.
 *
 * The scrollable branch carries three settings that are not cosmetic:
 *
 * - `keyboardShouldPersistTaps="handled"`. React Native's default is `'never'`,
 *   which makes the FIRST tap on a button with the keyboard open do nothing but
 *   dismiss the keyboard. The employee taps "Registrar", nothing happens, they
 *   tap again. That is the "no responde" report, and `'handled'` is the fix:
 *   the tap reaches the button and the keyboard closes in the same gesture.
 * - `keyboardDismissMode="on-drag"`. Scrolling away from a field puts the
 *   keyboard down without registering anything (D2).
 * - `KeyboardAvoidingView`. On iOS the keyboard overlays the window, so the
 *   final action would sit under it with no way to reach it; `padding` lifts the
 *   content. On Android the window itself resizes (`softwareKeyboardLayoutMode`
 *   defaults to `resize`), so adding a behaviour there would double-count the
 *   inset and leave a gap.
 */
export function Screen({
  children,
  testID,
  scrollable = false,
}: {
  children: React.ReactNode;
  testID?: string;
  /**
   * Wrap the screen in a ScrollView when the content is variable-height and may
   * overflow on small phones (a 12-animal picker with a 64pt row each). Keep `false`
   * for screens that own their own scrollers (MilkingScreen, EventsScreen) so nested
   * scrollers do not show.
   */
  scrollable?: boolean;
}) {
  const { theme: activeTheme } = useTheme();

  if (!scrollable) {
    return (
      <View testID={testID} style={[styles.screen, { backgroundColor: activeTheme.color.background }]}>
        {children}
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={[styles.screenScroll, { backgroundColor: activeTheme.color.background }]}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        testID={testID}
        style={[styles.screenScroll, { backgroundColor: activeTheme.color.background }]}
        contentContainerStyle={styles.screenScrollContent}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
      >
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/**
 * A list with a guaranteed minimum height so an empty list still takes the room the
 * employee expects — a screen where the picker collapses to zero is read as broken by
 * the person holding the phone. The action, if provided, is the next reasonable step
 * (usually "go to Sync"). Keeping the copy in Spanish matches the rest of the field UI.
 */
export function EmptyState({
  title,
  hint,
  action,
  testID,
}: {
  title: string;
  hint?: string;
  action?: { label: string; onPress: () => void };
  testID?: string;
}) {
  const { theme: activeTheme } = useTheme();

  return (
    <View
      testID={testID}
      style={[
        styles.empty,
        {
          borderColor: activeTheme.color.border,
          backgroundColor: activeTheme.color.surface,
        },
      ]}
    >
      <Text style={[styles.emptyTitle, { color: activeTheme.color.text, fontSize: activeTheme.font.body }]}>
        {title}
      </Text>
      {hint ? (
        <Text style={[styles.emptyHint, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
          {hint}
        </Text>
      ) : null}
      {action ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={action.label}
          onPress={action.onPress}
          style={({ pressed }) => [
            styles.emptyAction,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: activeTheme.color.surfaceRaised,
              opacity: pressed ? 0.8 : 1,
            },
          ]}
        >
          <Text style={[styles.emptyActionLabel, { color: activeTheme.color.text, fontSize: activeTheme.font.body }]}>
            {action.label}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

export function Title({ children, testID }: { children: React.ReactNode; testID?: string }) {
  const { theme: activeTheme } = useTheme();
  return (
    <Text testID={testID} style={[styles.title, { color: activeTheme.color.text, fontSize: activeTheme.font.title }]}>
      {children}
    </Text>
  );
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
  const { theme: activeTheme } = useTheme();
  return (
    <Text
      testID={testID}
      style={[
        styles.body,
        {
          fontSize: activeTheme.font.body,
          color: muted ? activeTheme.color.textMuted : activeTheme.color.text,
        },
      ]}
    >
      {children}
    </Text>
  );
}

export function Card({
  children,
  style,
  testID,
  onPress,
}: {
  children: React.ReactNode;
  style?: ViewStyle;
  testID?: string;
  /**
   * Optional: makes the card a pressable row. Used by TodayScreen for the
   * "lo que registré hoy" entries that route to a future corrections flow.
   */
  onPress?: () => void;
}) {
  const { theme: activeTheme } = useTheme();

  const cardStyle = [
    styles.card,
    {
      backgroundColor: activeTheme.color.surface,
      borderColor: activeTheme.color.border,
    },
    style,
  ];

  if (onPress) {
    return (
      <Pressable
        testID={testID}
        accessibilityRole="button"
        onPress={onPress}
        style={({ pressed }) => [...cardStyle, pressed && { opacity: 0.85 }]}
      >
        {children}
      </Pressable>
    );
  }
  return <View testID={testID} style={cardStyle}>{children}</View>;
}

/**
 * A message the employee must be able to act on. Errors are never swallowed into a
 * console log: the person in the paddock is the only one who can fix most of them.
 */
export function Notice({ text, tone = 'danger' }: { text: string; tone?: 'danger' | 'warning' }) {
  const { theme: activeTheme } = useTheme();
  const background = tone === 'danger' ? activeTheme.color.danger : activeTheme.color.warning;
  const color = tone === 'danger' ? activeTheme.color.dangerText : activeTheme.color.warningText;

  return (
    <View accessibilityRole="alert" style={[styles.notice, { backgroundColor: background }]}>
      <Text style={[styles.noticeText, { color, fontSize: activeTheme.font.label }]}>{text}</Text>
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
  const { theme: activeTheme } = useTheme();

  return (
    <View style={styles.field}>
      <Text style={[styles.fieldLabel, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
        {label}
      </Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        keyboardType="decimal-pad"
        placeholder={placeholder}
        placeholderTextColor={activeTheme.color.textMuted}
        style={[
          styles.input,
          {
            minHeight: activeTheme.touchTarget,
            borderColor: activeTheme.color.border,
            backgroundColor: activeTheme.color.surfaceRaised,
            color: activeTheme.color.text,
            fontSize: activeTheme.font.body,
          },
        ]}
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
  placeholder,
}: {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  testID?: string;
  secure?: boolean;
  autoCapitalize?: 'none' | 'sentences';
  placeholder?: string;
}) {
  const { theme: activeTheme } = useTheme();

  return (
    <View style={styles.field}>
      <Text style={[styles.fieldLabel, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
        {label}
      </Text>
      <TextInput
        testID={testID}
        accessibilityLabel={label}
        value={value}
        onChangeText={onChangeText}
        secureTextEntry={secure}
        autoCapitalize={autoCapitalize}
        placeholder={placeholder}
        placeholderTextColor={activeTheme.color.textMuted}
        style={[
          styles.input,
          {
            minHeight: activeTheme.touchTarget,
            borderColor: activeTheme.color.border,
            backgroundColor: activeTheme.color.surfaceRaised,
            color: activeTheme.color.text,
            fontSize: activeTheme.font.body,
          },
        ]}
      />
    </View>
  );
}

/**
 * Etiqueta de arete prominente para identificación inequívoca a distancia de brazo.
 * Si el animal no tiene arete, muestra 'Sin arete' en estilo diferenciado y no falla.
 */
export function TagBadge({
  tag,
  label,
  size = 'normal',
  tone = 'default',
  testID,
}: {
  tag?: string | null;
  label?: string;
  size?: 'normal' | 'large';
  tone?: 'default' | 'accent' | 'muted';
  testID?: string;
}) {
  const { theme: activeTheme } = useTheme();
  const isLarge = size === 'large';
  const displayTag = (tag ?? label ?? '').trim();
  const isUntagged = !displayTag || displayTag.toLowerCase() === 'sin arete';
  const textContent = isUntagged ? 'Sin arete' : displayTag;

  const backgroundColor =
    tone === 'accent'
      ? activeTheme.color.primary
      : isUntagged || tone === 'muted'
        ? activeTheme.color.surfaceRaised
        : activeTheme.color.surface;

  const borderColor =
    tone === 'accent'
      ? activeTheme.color.primary
      : activeTheme.color.border;

  const textColor =
    tone === 'accent'
      ? activeTheme.color.primaryText
      : isUntagged || tone === 'muted'
        ? activeTheme.color.textMuted
        : activeTheme.color.text;

  return (
    <View
      testID={testID}
      accessibilityRole="text"
      accessibilityLabel={`Arete ${textContent}`}
      style={[
        styles.tagBadge,
        {
          backgroundColor,
          borderColor,
          paddingHorizontal: isLarge ? activeTheme.space.md : activeTheme.space.sm,
          paddingVertical: isLarge ? activeTheme.space.xs + 2 : activeTheme.space.xs,
          borderStyle: isUntagged ? 'dashed' : 'solid',
        },
      ]}
    >
      <Text
        style={[
          styles.tagBadgeText,
          {
            color: textColor,
            fontSize: isLarge ? activeTheme.font.title : activeTheme.font.body,
            fontWeight: '800',
          },
        ]}
      >
        {textContent}
      </Text>
    </View>
  );
}

/**
 * Tipografía nítida para magnitudes numéricas y unidades de campo (ej. '4.5 L', '280 kg').
 * Enfatiza el valor con peso 800 y mantiene la unidad visible pero diferenciada.
 */
export function QuantityText({
  value,
  unit,
  size = 'normal',
  muted = false,
  color,
  testID,
}: {
  value: string | number;
  unit?: string;
  size?: 'normal' | 'large' | 'display';
  muted?: boolean;
  color?: string;
  testID?: string;
}) {
  const { theme: activeTheme } = useTheme();
  const resolvedColor = color ?? (muted ? activeTheme.color.textMuted : activeTheme.color.text);
  const unitColor = activeTheme.color.textMuted;

  const valueFontSize =
    size === 'display'
      ? activeTheme.font.display
      : size === 'large'
        ? activeTheme.font.title
        : activeTheme.font.body;

  const unitFontSize =
    size === 'display'
      ? activeTheme.font.subtitle
      : size === 'large'
        ? activeTheme.font.body
        : activeTheme.font.label;

  return (
    <View testID={testID} style={styles.quantityContainer}>
      <Text style={[styles.quantityValue, { color: resolvedColor, fontSize: valueFontSize }]}>
        {value}
      </Text>
      {unit ? (
        <Text style={[styles.quantityUnit, { color: unitColor, fontSize: unitFontSize }]}>
          {` ${unit}`}
        </Text>
      ) : null}
    </View>
  );
}

export type StatusBadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

/**
 * Píldora de estado (ej. 'Retiro', 'Preñez', 'Local', 'Enviando', 'Aceptado').
 */
export function StatusBadge({
  label,
  tone = 'neutral',
  testID,
}: {
  label: string;
  tone?: StatusBadgeTone;
  testID?: string;
}) {
  const { theme: activeTheme } = useTheme();

  const background =
    tone === 'success'
      ? activeTheme.color.primary
      : tone === 'warning'
        ? activeTheme.color.warning
        : tone === 'danger'
          ? activeTheme.color.danger
          : tone === 'info'
            ? activeTheme.color.info
            : activeTheme.color.surfaceRaised;

  const textColor =
    tone === 'success'
      ? activeTheme.color.primaryText
      : tone === 'warning'
        ? activeTheme.color.warningText
        : tone === 'danger'
          ? activeTheme.color.dangerText
          : tone === 'info'
            ? '#FFFFFF'
            : activeTheme.color.text;

  return (
    <View
      testID={testID}
      accessibilityRole="text"
      style={[
        styles.statusBadge,
        {
          backgroundColor: background,
          borderColor: tone === 'neutral' ? activeTheme.color.border : 'transparent',
        },
      ]}
    >
      <Text style={[styles.statusBadgeText, { color: textColor, fontSize: activeTheme.font.micro }]}>
        {label}
      </Text>
    </View>
  );
}

/**
 * Sección de lista para agrupar colecciones sin convertir cada elemento en una tarjeta pesada.
 */
export function ListSection({
  title,
  subtitle,
  action,
  children,
  testID,
  style,
}: {
  title?: string;
  subtitle?: string;
  action?: { label: string; onPress: () => void };
  children: React.ReactNode;
  testID?: string;
  style?: ViewStyle;
}) {
  const { theme: activeTheme } = useTheme();

  return (
    <View testID={testID} style={[styles.listSection, style]}>
      {title || subtitle || action ? (
        <View style={styles.listSectionHeader}>
          <View style={styles.listSectionTitleGroup}>
            {title ? (
              <Text style={[styles.listSectionTitle, { color: activeTheme.color.text, fontSize: activeTheme.font.subtitle }]}>
                {title}
              </Text>
            ) : null}
            {subtitle ? (
              <Text style={[styles.listSectionSubtitle, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
                {subtitle}
              </Text>
            ) : null}
          </View>
          {action ? (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={action.label}
              onPress={action.onPress}
              style={({ pressed }) => [styles.listSectionAction, { opacity: pressed ? 0.7 : 1 }]}
            >
              <Text style={[styles.listSectionActionText, { color: activeTheme.color.primary, fontSize: activeTheme.font.label }]}>
                {action.label}
              </Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}
      <View
        style={[
          styles.listSectionContent,
          {
            backgroundColor: activeTheme.color.surface,
            borderColor: activeTheme.color.border,
          },
        ]}
      >
        {children}
      </View>
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
  screenScroll: {
    flex: 1,
    backgroundColor: theme.color.background,
  },
  /**
   * `flexGrow`, never `flex`. `flex: 1` clamps the content box to the viewport
   * height, which is exactly how a screen ends up with its last button below the
   * fold and no way to scroll to it; `flexGrow: 1` still fills a short screen but
   * lets a long one grow past the window.
   */
  screenScrollContent: {
    flexGrow: 1,
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
  empty: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: theme.space.lg,
    paddingVertical: theme.space.xl,
    gap: theme.space.sm,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.color.border,
    borderStyle: 'dashed',
    backgroundColor: theme.color.surface,
  },
  emptyTitle: {
    color: theme.color.text,
    fontSize: theme.font.body,
    fontWeight: '700',
    textAlign: 'center',
  },
  emptyHint: {
    color: theme.color.textMuted,
    fontSize: theme.font.label,
    textAlign: 'center',
  },
  emptyAction: {
    marginTop: theme.space.sm,
    minHeight: theme.touchTarget,
    borderRadius: theme.radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: theme.space.lg,
    backgroundColor: theme.color.surfaceRaised,
  },
  emptyActionLabel: {
    color: theme.color.text,
    fontSize: theme.font.body,
    fontWeight: '700',
  },
  tagBadge: {
    borderRadius: theme.radius.md,
    borderWidth: 1.5,
    alignSelf: 'flex-start',
    justifyContent: 'center',
  },
  tagBadgeText: {
    letterSpacing: 0.5,
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'baseline',
  },
  quantityValue: {
    fontWeight: '800',
  },
  quantityUnit: {
    fontWeight: '600',
  },
  statusBadge: {
    borderRadius: theme.radius.md,
    borderWidth: 1,
    paddingHorizontal: theme.space.sm,
    paddingVertical: theme.space.xs,
    alignSelf: 'flex-start',
    justifyContent: 'center',
  },
  statusBadgeText: {
    fontWeight: '700',
    letterSpacing: 0.2,
  },
  listSection: {
    gap: theme.space.sm,
  },
  listSectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    paddingHorizontal: theme.space.xs,
  },
  listSectionTitleGroup: {
    flex: 1,
    gap: 2,
  },
  listSectionTitle: {
    fontWeight: '700',
  },
  listSectionSubtitle: {
    fontWeight: '500',
  },
  listSectionAction: {
    paddingVertical: theme.space.xs,
    paddingHorizontal: theme.space.sm,
  },
  listSectionActionText: {
    fontWeight: '700',
  },
  listSectionContent: {
    borderRadius: theme.radius.md,
    borderWidth: 1,
    overflow: 'hidden',
  },
});
