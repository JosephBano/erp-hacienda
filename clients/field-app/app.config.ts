import { ExpoConfig, ConfigContext } from 'expo/config';

/**
 * PENDIENTE DE CONFIRMACIÓN: INV.1 (inventario del teléfono en campo).
 * El package prod es com.joemandev.hatofieldapp salvo que el inventario diga otra cosa.
 * Se preserva esta identidad para asegurar que la actualización no rompa la app instalada
 * ni su base de datos local SQLite / outbox.
 */
export const PROD_PACKAGE = 'com.joemandev.hatofieldapp';
export const STAGE_PACKAGE = 'com.joemandev.hatofieldapp.stage';

export type AppVariant = 'development' | 'stage' | 'production';

export const ALLOWED_API_URLS: Record<AppVariant, string[]> = {
  development: [
    'http://10.0.2.2:5282',
    'http://localhost:5282',
    'http://100.101.240.44:5282',
    'http://100.121.210.68:8080',
  ],
  stage: [
    'https://joemanserver.ts.net/api',
    'http://100.121.210.68:8080',
    'http://100.101.240.44:5282',
  ],
  production: [
    'https://hato-oracle.ts.net/api',
    'https://hato-prod.ts.net/api',
    'http://100.121.210.68:8080',
  ],
};

export const DEFAULT_API_URLS: Record<AppVariant, string> = {
  development: 'http://10.0.2.2:5282',
  stage: 'https://joemanserver.ts.net/api',
  production: 'https://hato-oracle.ts.net/api',
};

export function resolveVariant(rawVariant?: string): AppVariant {
  const variant = (rawVariant ?? process.env.APP_VARIANT ?? process.env.EXPO_PUBLIC_APP_VARIANT ?? 'development').toLowerCase();
  if (variant === 'production' || variant === 'prod') {
    return 'production';
  }
  if (variant === 'stage' || variant === 'staging' || variant === 'preview') {
    return 'stage';
  }
  return 'development';
}

export function resolveApiUrl(variant: AppVariant, inputUrl?: string): string {
  const url = inputUrl ?? process.env.EXPO_PUBLIC_API_URL;
  const allowed = ALLOWED_API_URLS[variant];

  if (!url) {
    return DEFAULT_API_URLS[variant];
  }

  if (!allowed.includes(url)) {
    throw new Error(
      `[SECURITY] API URL '${url}' no está en la allowlist para la variante '${variant}'. ` +
      `URLs permitidas: ${allowed.join(', ')}`
    );
  }

  return url;
}

export default ({ config }: ConfigContext): ExpoConfig => {
  const variant = resolveVariant();
  const apiUrl = resolveApiUrl(variant);

  const isProd = variant === 'production';
  const isStage = variant === 'stage';

  const name = isProd ? 'HATO Campo' : isStage ? 'HATO Campo STAGE' : 'HATO Campo DEV';
  const androidPackage = isProd ? PROD_PACKAGE : STAGE_PACKAGE;
  const iconBackgroundColor = isProd ? '#121824' : isStage ? '#382004' : '#121824';

  return {
    ...config,
    name,
    slug: 'hato-field-app',
    version: config.version ?? '1.0.0',
    android: {
      ...config.android,
      package: androidPackage,
      adaptiveIcon: {
        foregroundImage: './assets/adaptive-icon.png',
        backgroundColor: iconBackgroundColor,
      },
    },
    ios: {
      ...config.ios,
      bundleIdentifier: androidPackage,
    },
    extra: {
      ...config.extra,
      appVariant: variant,
      apiUrl,
    },
  };
};
