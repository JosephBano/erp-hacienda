import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { Pressable, Text, View } from 'react-native';

import {
  BigButton,
  Card,
  ListSection,
  Notice,
  NumberField,
  QuantityText,
  Screen,
  StatusBadge,
  TagBadge,
  TextField,
} from '../src/ui/components';
import {
  createTheme,
  darkPalette,
  font,
  getContrastRatio,
  getRelativeLuminance,
  getStoredThemePreference,
  lightPalette,
  radius,
  setStoredThemePreference,
  space,
  theme,
  ThemeProvider,
  touchTarget,
  useTheme,
} from '../src/ui/theme';

const flatten = (style: unknown): Record<string, unknown> =>
  Array.isArray(style)
    ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
    : ((style ?? {}) as Record<string, unknown>);

describe('Visual System — Design tokens and ergonomics', () => {
  beforeEach(() => {
    (global as any).__resetNativeMocks?.();
  });

  describe('Tokens and backward compatibility (T1.1 & T1.5)', () => {
    it('preserves existing theme export structure and defaults', () => {
      expect(theme.touchTarget).toBe(64);
      expect(touchTarget).toBe(64);

      expect(theme.space.xs).toBe(4);
      expect(theme.space.sm).toBe(8);
      expect(theme.space.md).toBe(16);
      expect(theme.space.lg).toBe(24);
      expect(theme.space.xl).toBe(32);

      expect(theme.radius.md).toBe(12);
      expect(theme.radius.lg).toBe(20);

      expect(theme.font.body).toBe(18);
      expect(theme.font.label).toBe(16);
      expect(theme.font.title).toBe(26);
      expect(theme.font.display).toBe(40);
      expect(theme.font.micro).toBe(14);
      expect(theme.font.subtitle).toBe(20);

      // Default color export preserves dark palette
      expect(theme.color.background).toBe(darkPalette.background);
      expect(theme.color.surface).toBe(darkPalette.surface);
      expect(theme.color.text).toBe(darkPalette.text);
    });

    it('defines the warm light palette for Ecuadorian outdoor sun (T1.2)', () => {
      expect(lightPalette.background).toBe('#FAF8F5');
      expect(lightPalette.surface).toBe('#FFFFFF');
      expect(lightPalette.surfaceRaised).toBe('#F2EFE9');
      expect(lightPalette.border).toBe('#D1C7B7');
      expect(lightPalette.text).toBe('#111827');
      expect(lightPalette.textMuted).toBe('#6B7280');
      expect(lightPalette.primary).toBe('#1E6F3E');
      expect(lightPalette.primaryText).toBe('#FFFFFF');
      expect(lightPalette.warning).toBe('#D97706');
      expect(lightPalette.warningText).toBe('#241701');
      expect(lightPalette.danger).toBe('#DC2626');
      expect(lightPalette.dangerText).toBe('#FFFFFF');
      expect(lightPalette.info).toBe('#2563EB');
    });

    it('defines the dark palette for barn and 5 AM low light (T1.3)', () => {
      expect(darkPalette.background).toBe('#0B1220');
      expect(darkPalette.surface).toBe('#16213A');
      expect(darkPalette.surfaceRaised).toBe('#1E2C4A');
      expect(darkPalette.border).toBe('#33456B');
      expect(darkPalette.text).toBe('#FFFFFF');
      expect(darkPalette.textMuted).toBe('#B9C6E0');
      expect(darkPalette.primary).toBe('#2FA84F');
      expect(darkPalette.primaryText).toBe('#04140A');
      expect(darkPalette.warning).toBe('#F5A524');
      expect(darkPalette.warningText).toBe('#241701');
      expect(darkPalette.danger).toBe('#E5484D');
      expect(darkPalette.dangerText).toBe('#FFFFFF');
      expect(darkPalette.info).toBe('#3B82F6');
    });
  });

  describe('Contrast ratios (WCAG AA) (T1.9)', () => {
    it('verifies luminance calculation and ratio symmetry', () => {
      expect(getRelativeLuminance('#000000')).toBeCloseTo(0, 3);
      expect(getRelativeLuminance('#FFFFFF')).toBeCloseTo(1, 3);
      expect(getContrastRatio('#FFFFFF', '#000000')).toBeCloseTo(21, 0);
      expect(getContrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 0);
    });

    it('exceeds 4.5:1 contrast for all text combinations in Light Theme', () => {
      // Primary text on primary action (6.19:1)
      expect(getContrastRatio(lightPalette.primaryText, lightPalette.primary)).toBeGreaterThanOrEqual(4.5);

      // Warning text on warning badge/notice (5.60:1)
      expect(getContrastRatio(lightPalette.warningText, lightPalette.warning)).toBeGreaterThanOrEqual(4.5);

      // Danger text on danger action/notice (4.85:1)
      expect(getContrastRatio(lightPalette.dangerText, lightPalette.danger)).toBeGreaterThanOrEqual(4.5);

      // Normal text on canvas and surfaces (> 14:1)
      expect(getContrastRatio(lightPalette.text, lightPalette.background)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(lightPalette.text, lightPalette.surface)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(lightPalette.text, lightPalette.surfaceRaised)).toBeGreaterThanOrEqual(4.5);

      // Muted metadata text on canvas and surface (>= 4.5:1)
      expect(getContrastRatio(lightPalette.textMuted, lightPalette.background)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(lightPalette.textMuted, lightPalette.surface)).toBeGreaterThanOrEqual(4.5);
    });

    it('exceeds WCAG AA contrast for text combinations in Dark Theme', () => {
      // Primary text on primary action (6.43:1)
      expect(getContrastRatio(darkPalette.primaryText, darkPalette.primary)).toBeGreaterThanOrEqual(4.5);

      // Warning text on warning badge/notice (8.46:1)
      expect(getContrastRatio(darkPalette.warningText, darkPalette.warning)).toBeGreaterThanOrEqual(4.5);

      // Normal text on canvas and surfaces (> 13:1)
      expect(getContrastRatio(darkPalette.text, darkPalette.background)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(darkPalette.text, darkPalette.surface)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(darkPalette.text, darkPalette.surfaceRaised)).toBeGreaterThanOrEqual(4.5);

      // Muted metadata text on canvas and surfaces (> 7:1)
      expect(getContrastRatio(darkPalette.textMuted, darkPalette.background)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(darkPalette.textMuted, darkPalette.surface)).toBeGreaterThanOrEqual(4.5);
      expect(getContrastRatio(darkPalette.textMuted, darkPalette.surfaceRaised)).toBeGreaterThanOrEqual(4.5);

      // Danger indicator against dark background (4.78:1)
      expect(getContrastRatio(darkPalette.danger, darkPalette.background)).toBeGreaterThanOrEqual(4.5);

      // Danger button text (18pt bold large control, WCAG AA requirement >= 3.0:1, achieves 3.92:1)
      expect(getContrastRatio(darkPalette.dangerText, darkPalette.danger)).toBeGreaterThanOrEqual(3.0);
    });
  });

  describe('Theme preference and switching (T1.4)', () => {
    function ThemeTester() {
      const { theme: activeTheme, themeMode, resolvedTheme, setThemeMode, isDark } = useTheme();

      return (
        <View testID="theme-consumer">
          <Text testID="theme-mode">{themeMode}</Text>
          <Text testID="resolved-theme">{resolvedTheme}</Text>
          <Text testID="is-dark">{isDark ? 'true' : 'false'}</Text>
          <Text testID="bg-color">{activeTheme.color.background}</Text>

          <Pressable testID="btn-light" onPress={() => setThemeMode('light')}>
            <Text>Set Light</Text>
          </Pressable>
          <Pressable testID="btn-dark" onPress={() => setThemeMode('dark')}>
            <Text>Set Dark</Text>
          </Pressable>
          <Pressable testID="btn-system" onPress={() => setThemeMode('system')}>
            <Text>Set System</Text>
          </Pressable>
        </View>
      );
    }

    it('provides safe fallback when used outside ThemeProvider', async () => {
      await render(<ThemeTester />);
      expect(screen.getByTestId('theme-mode').props.children).toBe('system');
      expect(screen.getByTestId('bg-color').props.children).toBe(darkPalette.background);
    });

    it('switches between light and dark themes dynamically', async () => {
      await render(
        <ThemeProvider initialMode="light">
          <ThemeTester />
        </ThemeProvider>,
      );

      expect(screen.getByTestId('theme-mode').props.children).toBe('light');
      expect(screen.getByTestId('resolved-theme').props.children).toBe('light');
      expect(screen.getByTestId('is-dark').props.children).toBe('false');
      expect(screen.getByTestId('bg-color').props.children).toBe(lightPalette.background);

      // Switch to dark
      fireEvent.press(screen.getByTestId('btn-dark'));
      await waitFor(() => expect(screen.getByTestId('theme-mode').props.children).toBe('dark'));
      expect(screen.getByTestId('resolved-theme').props.children).toBe('dark');
      expect(screen.getByTestId('is-dark').props.children).toBe('true');
      expect(screen.getByTestId('bg-color').props.children).toBe(darkPalette.background);

      // Switch back to light
      fireEvent.press(screen.getByTestId('btn-light'));
      await waitFor(() => expect(screen.getByTestId('theme-mode').props.children).toBe('light'));
      expect(screen.getByTestId('resolved-theme').props.children).toBe('light');
      expect(screen.getByTestId('bg-color').props.children).toBe(lightPalette.background);
    });

    it('persists theme preference locally without requiring network', async () => {
      await setStoredThemePreference('light');
      const loaded = await getStoredThemePreference();
      expect(loaded).toBe('light');

      await setStoredThemePreference('dark');
      expect(await getStoredThemePreference()).toBe('dark');
    });
  });

  describe('UI Components (T1.6 & T1.8)', () => {
    describe('TagBadge', () => {
      it('renders animal ear tag code prominently with high legibility', async () => {
        await render(<TagBadge testID="tag-007" tag="007" />);

        const badge = screen.getByTestId('tag-007');
        expect(badge.props.accessibilityLabel).toBe('Arete 007');
        expect(screen.getByText('007')).toBeTruthy();
      });

      it('renders fallback for untagged animal without crashing', async () => {
        await render(<TagBadge testID="tag-empty" tag="" />);

        expect(screen.getByTestId('tag-empty').props.accessibilityLabel).toBe('Arete Sin arete');
        expect(screen.getByText('Sin arete')).toBeTruthy();
      });

      it('supports large size for animal detail header', async () => {
        await render(<TagBadge testID="tag-large" tag="ES-402" size="large" />);

        const text = screen.getByText('ES-402');
        expect(flatten(text.props.style).fontSize).toBe(font.title);
      });
    });

    describe('QuantityText', () => {
      it('renders numeric magnitude and unit with differentiated typography', async () => {
        await render(<QuantityText testID="milk-qty" value={4.5} unit="L" />);

        expect(screen.getByText('4.5')).toBeTruthy();
        expect(screen.getByText(' L')).toBeTruthy();
      });

      it('supports display and large sizes', async () => {
        await render(<QuantityText testID="weight-display" value="280" unit="kg" size="display" />);

        const valueText = screen.getByText('280');
        expect(flatten(valueText.props.style).fontSize).toBe(font.display);
      });
    });

    describe('StatusBadge', () => {
      it('renders various semantic status tones', async () => {
        await render(
          <View>
            <StatusBadge testID="badge-success" label="Al día" tone="success" />
            <StatusBadge testID="badge-warning" label="Retiro" tone="warning" />
            <StatusBadge testID="badge-danger" label="Rechazado" tone="danger" />
            <StatusBadge testID="badge-info" label="Local" tone="info" />
            <StatusBadge testID="badge-neutral" label="Borrador" tone="neutral" />
          </View>,
        );

        expect(screen.getByText('Al día')).toBeTruthy();
        expect(screen.getByText('Retiro')).toBeTruthy();
        expect(screen.getByText('Rechazado')).toBeTruthy();
        expect(screen.getByText('Local')).toBeTruthy();
        expect(screen.getByText('Borrador')).toBeTruthy();
      });
    });

    describe('ListSection', () => {
      it('renders collection section with title, subtitle, action, and grouped items', async () => {
        const onAction = jest.fn();
        await render(
          <ListSection
            testID="section-events"
            title="Eventos recientes"
            subtitle="Últimos registros de hoy"
            action={{ label: 'Ver todos', onPress: onAction }}
          >
            <Text>Item 1</Text>
            <Text>Item 2</Text>
          </ListSection>,
        );

        expect(screen.getByText('Eventos recientes')).toBeTruthy();
        expect(screen.getByText('Últimos registros de hoy')).toBeTruthy();
        expect(screen.getByText('Item 1')).toBeTruthy();
        expect(screen.getByText('Item 2')).toBeTruthy();

        fireEvent.press(screen.getByText('Ver todos'));
        expect(onAction).toHaveBeenCalledTimes(1);
      });
    });

    describe('Touch Targets (T1.5)', () => {
      it('enforces touchTarget >= 64 logical units on primary field controls', async () => {
        await render(
          <View>
            <BigButton testID="btn-action" label="Registrar" />
            <NumberField testID="field-qty" label="Litros" value="10" onChangeText={() => {}} />
            <TextField testID="field-notes" label="Notas" value="Observación" onChangeText={() => {}} />
          </View>,
        );

        const btnStyle = flatten(screen.getByTestId('btn-action').props.style);
        expect(btnStyle.minHeight).toBeGreaterThanOrEqual(64);

        const numInputStyle = flatten(screen.getByTestId('field-qty').props.style);
        expect(numInputStyle.minHeight).toBeGreaterThanOrEqual(64);

        const textInputStyle = flatten(screen.getByTestId('field-notes').props.style);
        expect(textInputStyle.minHeight).toBeGreaterThanOrEqual(64);
      });
    });
  });
});
