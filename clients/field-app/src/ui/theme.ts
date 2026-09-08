import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useColorScheme } from 'react-native';
import * as SecureStore from 'expo-secure-store';

export interface ThemeColors {
  readonly background: string;
  readonly surface: string;
  readonly surfaceRaised: string;
  readonly border: string;
  readonly text: string;
  readonly textMuted: string;
  readonly primary: string;
  readonly primaryText: string;
  readonly warning: string;
  readonly warningText: string;
  readonly danger: string;
  readonly dangerText: string;
  readonly info: string;
}

/**
 * Paleta para uso en exteriores a pleno sol ecuatoriano.
 * Superficies cálidas tipo pergamino, texto casi negro y verde profundo de acento.
 */
export const lightPalette: ThemeColors = {
  background: '#FAF8F5',
  surface: '#FFFFFF',
  surfaceRaised: '#F2EFE9',
  border: '#D1C7B7',
  text: '#111827',
  textMuted: '#6B7280',
  primary: '#1E6F3E',
  primaryText: '#FFFFFF',
  warning: '#D97706',
  warningText: '#241701',
  danger: '#DC2626',
  dangerText: '#FFFFFF',
  info: '#2563EB',
} as const;

/**
 * Paleta para uso en galpón y madrugada (5:00 AM / poca luz).
 * Fondo noche profundo, texto blanco brillante y acentos de alta reflectancia.
 */
export const darkPalette: ThemeColors = {
  background: '#0B1220',
  surface: '#16213A',
  surfaceRaised: '#1E2C4A',
  border: '#33456B',
  text: '#FFFFFF',
  textMuted: '#B9C6E0',
  primary: '#2FA84F',
  primaryText: '#04140A',
  warning: '#F5A524',
  warningText: '#241701',
  danger: '#E5484D',
  dangerText: '#FFFFFF',
  info: '#3B82F6',
} as const;

export const palettes = {
  light: lightPalette,
  dark: darkPalette,
} as const;

export type ThemeMode = 'system' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

export const space = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

export const radius = {
  md: 12,
  lg: 20,
} as const;

export const font = {
  micro: 14,
  label: 16,
  body: 18,
  subtitle: 20,
  title: 26,
  display: 40,
} as const;

/**
 * Altura mínima para elementos táctiles de campo.
 * Debajo de 64pt, una mano enguantada o mojada falla sistemáticamente.
 */
export const touchTarget = 64;

export const color = darkPalette;

/**
 * Design tokens for a screen used at 5 AM, outdoors, with gloves on.
 * Default export preserves the dark palette for backward compatibility.
 */
export const theme = {
  color: darkPalette,
  space,
  radius,
  font,
  touchTarget,
} as const;

export type AppTheme = {
  color: ThemeColors;
  space: typeof space;
  radius: typeof radius;
  font: typeof font;
  touchTarget: number;
};

export function createTheme(resolvedTheme: ResolvedTheme): AppTheme {
  return {
    color: resolvedTheme === 'dark' ? darkPalette : lightPalette,
    space,
    radius,
    font,
    touchTarget,
  };
}

export const THEME_STORAGE_KEY = 'hato_theme_preference';

let inMemoryPreference: ThemeMode = 'system';

/**
 * Lee la preferencia de tema guardada en el dispositivo.
 * 100% offline: usa SecureStore si está disponible, localStorage en web o memoria local.
 */
export async function getStoredThemePreference(): Promise<ThemeMode> {
  try {
    if (typeof SecureStore?.getItemAsync === 'function') {
      const stored = await SecureStore.getItemAsync(THEME_STORAGE_KEY);
      if (stored === 'light' || stored === 'dark' || stored === 'system') {
        return stored;
      }
    }
  } catch {
    // Continúa con fallback
  }

  if (typeof localStorage !== 'undefined') {
    try {
      const stored = localStorage.getItem(THEME_STORAGE_KEY);
      if (stored === 'light' || stored === 'dark' || stored === 'system') {
        return stored;
      }
    } catch {
      // Ignora errores de storage
    }
  }

  return inMemoryPreference;
}

/**
 * Guarda la preferencia de tema de forma local e inmediata.
 * Ninguna variante requiere conexión ni llamadas a red.
 */
export async function setStoredThemePreference(mode: ThemeMode): Promise<void> {
  inMemoryPreference = mode;

  try {
    if (typeof SecureStore?.setItemAsync === 'function') {
      await SecureStore.setItemAsync(THEME_STORAGE_KEY, mode);
    }
  } catch {
    // Continúa con fallback
  }

  if (typeof localStorage !== 'undefined') {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, mode);
    } catch {
      // Ignora errores de storage
    }
  }
}

export interface ThemeContextValue {
  theme: AppTheme;
  color: ThemeColors;
  themeMode: ThemeMode;
  resolvedTheme: ResolvedTheme;
  setThemeMode: (mode: ThemeMode) => Promise<void>;
  isDark: boolean;
}

const defaultThemeContext: ThemeContextValue = {
  theme,
  color: theme.color,
  themeMode: 'system',
  resolvedTheme: 'dark',
  setThemeMode: async () => {},
  isDark: true,
};

export const ThemeContext = createContext<ThemeContextValue>(defaultThemeContext);

export function ThemeProvider({
  children,
  initialMode,
}: {
  children: React.ReactNode;
  initialMode?: ThemeMode;
}) {
  const systemColorScheme = useColorScheme();
  const [themeMode, setThemeModeState] = useState<ThemeMode>(initialMode ?? 'system');

  useEffect(() => {
    let active = true;
    if (!initialMode) {
      getStoredThemePreference().then((stored) => {
        if (active && stored) {
          setThemeModeState(stored);
        }
      });
    }
    return () => {
      active = false;
    };
  }, [initialMode]);

  const resolvedTheme: ResolvedTheme = useMemo(() => {
    if (themeMode === 'light') return 'light';
    if (themeMode === 'dark') return 'dark';
    return systemColorScheme === 'dark' ? 'dark' : 'light';
  }, [themeMode, systemColorScheme]);

  const activeTheme = useMemo(() => createTheme(resolvedTheme), [resolvedTheme]);

  const setThemeMode = useCallback(async (mode: ThemeMode) => {
    setThemeModeState(mode);
    await setStoredThemePreference(mode);
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme: activeTheme,
      color: activeTheme.color,
      themeMode,
      resolvedTheme,
      setThemeMode,
      isDark: resolvedTheme === 'dark',
    }),
    [activeTheme, themeMode, resolvedTheme, setThemeMode],
  );

  return React.createElement(ThemeContext.Provider, { value }, children);
}

/**
 * Hook para consumir el tema activo en cualquier componente.
 * Si se invoca fuera de ThemeProvider, devuelve el tema predeterminado sin romperse.
 */
export function useTheme(): ThemeContextValue {
  return useContext(ThemeContext);
}

/**
 * Calcula la luminancia relativa según especificación WCAG 2.1.
 */
export function getRelativeLuminance(hexColor: string): number {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substring(0, 2), 16) / 255;
  const g = parseInt(hex.substring(2, 4), 16) / 255;
  const b = parseInt(hex.substring(4, 6), 16) / 255;

  const toLinear = (c: number) =>
    c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);

  return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b);
}

/**
 * Calcula el ratio de contraste WCAG entre dos colores hex.
 * Para cumplir WCAG AA en texto normal, debe ser >= 4.5:1.
 */
export function getContrastRatio(colorA: string, colorB: string): number {
  const lumA = getRelativeLuminance(colorA);
  const lumB = getRelativeLuminance(colorB);
  const lighter = Math.max(lumA, lumB);
  const darker = Math.min(lumA, lumB);
  return (lighter + 0.05) / (darker + 0.05);
}
