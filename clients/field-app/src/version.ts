export interface VersionInfo {
  version: string;
  commit?: string;
  buildDate?: string;
}

let versionInfo: VersionInfo = {
  version: '0.3.0',
};

try {
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const pkg = require('../../package.json');
  versionInfo = {
    version: pkg.version || '0.3.0',
    commit: pkg.buildMetadata?.commit,
    buildDate: pkg.buildMetadata?.buildDate,
  };
} catch {
  // fallback default
}

export const APP_VERSION = versionInfo;
