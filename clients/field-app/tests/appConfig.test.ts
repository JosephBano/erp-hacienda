import appConfig, {
  resolveVariant,
  resolveApiUrl,
  PROD_PACKAGE,
  STAGE_PACKAGE,
  ALLOWED_API_URLS,
  DEFAULT_API_URLS,
} from '../app.config';

describe('Feature-0014: Mobile App Configuration (app.config.ts)', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    jest.resetModules();
    process.env = { ...originalEnv };
  });

  afterAll(() => {
    process.env = originalEnv;
  });

  describe('resolveVariant', () => {
    it('resolves production variant from APP_VARIANT or EXPO_PUBLIC_APP_VARIANT', () => {
      expect(resolveVariant('production')).toBe('production');
      expect(resolveVariant('prod')).toBe('production');

      process.env.APP_VARIANT = 'production';
      expect(resolveVariant()).toBe('production');
    });

    it('resolves stage variant from APP_VARIANT, staging, or preview', () => {
      expect(resolveVariant('stage')).toBe('stage');
      expect(resolveVariant('staging')).toBe('stage');
      expect(resolveVariant('preview')).toBe('stage');

      process.env.APP_VARIANT = 'stage';
      expect(resolveVariant()).toBe('stage');
    });

    it('defaults to development when not specified', () => {
      delete process.env.APP_VARIANT;
      delete process.env.EXPO_PUBLIC_APP_VARIANT;
      expect(resolveVariant()).toBe('development');
    });
  });

  describe('resolveApiUrl and Allowlist Enforcement', () => {
    it('returns default API URL when none is specified', () => {
      expect(resolveApiUrl('production')).toBe(DEFAULT_API_URLS.production);
      expect(resolveApiUrl('stage')).toBe(DEFAULT_API_URLS.stage);
      expect(resolveApiUrl('development')).toBe(DEFAULT_API_URLS.development);
    });

    it('accepts explicitly allowed URLs for each variant', () => {
      for (const url of ALLOWED_API_URLS.stage) {
        expect(resolveApiUrl('stage', url)).toBe(url);
      }
      for (const url of ALLOWED_API_URLS.production) {
        expect(resolveApiUrl('production', url)).toBe(url);
      }
    });

    it('rejects unlisted or arbitrary URLs with a security error', () => {
      expect(() =>
        resolveApiUrl('stage', 'https://malicious-server.example.com/api')
      ).toThrow(/\[SECURITY\] API URL/);

      expect(() =>
        resolveApiUrl('production', 'http://untrusted-host:8080')
      ).toThrow(/\[SECURITY\] API URL/);
    });
  });

  describe('appConfig generation', () => {
    const baseConfig = {
      name: 'Base App',
      slug: 'hato-field-app',
      version: '1.2.3',
    };

    it('generates production config preserving confirmed production package', () => {
      process.env.APP_VARIANT = 'production';
      const config = appConfig({ config: baseConfig } as any);

      expect(config.name).toBe('HATO Campo');
      expect(config.android?.package).toBe(PROD_PACKAGE);
      expect(config.ios?.bundleIdentifier).toBe(PROD_PACKAGE);
      expect(config.extra?.appVariant).toBe('production');
      expect(config.extra?.apiUrl).toBe(DEFAULT_API_URLS.production);
    });

    it('generates stage config with separate stage package and distinct name', () => {
      process.env.APP_VARIANT = 'stage';
      const config = appConfig({ config: baseConfig } as any);

      expect(config.name).toBe('HATO Campo STAGE');
      expect(config.android?.package).toBe(STAGE_PACKAGE);
      expect(config.ios?.bundleIdentifier).toBe(STAGE_PACKAGE);
      expect(config.extra?.appVariant).toBe('stage');
      expect(config.extra?.apiUrl).toBe(DEFAULT_API_URLS.stage);
    });

    it('ensures stage and production packages are distinct to allow coexistence', () => {
      expect(PROD_PACKAGE).not.toBe(STAGE_PACKAGE);
      expect(STAGE_PACKAGE).toBe('com.joemandev.hatofieldapp.stage');
      expect(PROD_PACKAGE).toBe('com.joemandev.hatofieldapp');
    });
  });
});
