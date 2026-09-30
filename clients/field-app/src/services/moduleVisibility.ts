import { Database, Q } from '@nozbe/watermelondb';

import { FarmModule } from '../database/models';
import type { SyncApi } from './syncApi';

/**
 * Common contract for module rows evaluated in visibility checks.
 */
export interface ModuleRow {
  key: string;
  enabled: boolean;
}

/**
 * Normalizes a module key: trims leading/trailing whitespace and converts to lowercase.
 * Matches panel implementation: "Inventory.X " -> "inventory.x".
 */
export function normalizeModuleKey(raw: string): string {
  return raw.trim().toLowerCase();
}

/**
 * Evaluates visibility along the key chain (ADR-0042 / D12).
 *
 * Conjunction rule: a module or submodule is visible if and only if EVERY segment
 * along its dotted path is enabled:
 *   isVisible("a.b.c") = on("a") && on("a.b") && on("a.b.c")
 *
 * Fail-open policy (S2): if any key in the chain has no row in the database,
 * it is treated as enabled (true).
 */
export function isVisible(key: string, rows: readonly ModuleRow[]): boolean {
  const normalizedKey = normalizeModuleKey(key);
  if (!normalizedKey) {
    return true;
  }

  const rowMap = new Map<string, boolean>();
  for (const row of rows) {
    rowMap.set(normalizeModuleKey(row.key), row.enabled);
  }

  const parts = normalizedKey.split('.');
  let currentKey = '';
  for (let i = 0; i < parts.length; i++) {
    currentKey = i === 0 ? parts[i] : `${currentKey}.${parts[i]}`;
    const enabled = rowMap.get(currentKey);
    if (enabled !== undefined && !enabled) {
      return false;
    }
  }

  return true;
}

/**
 * Validated module key. No longer a closed union, allowing hierarchical submodule keys.
 */
export type ModuleKey = string;

/**
 * Tells the navigation layer whether a module's entry should be visible right now.
 *
 * Art. 9 lives here: the flag comes from rows the pull already wrote to the local DB,
 * never from a network call. A visibility decision that flickered with signal would
 * defeat the whole point of being offline-first.
 *
 * Default behaviour: a module with no row in the table is treated as *visible* (S2 fail-open).
 */
export class ModuleVisibility {
  constructor(
    private readonly database: Database,
    private readonly api?: SyncApi,
  ) {}

  async canShow(key: ModuleKey): Promise<boolean> {
    const normalizedKey = normalizeModuleKey(key);
    if (!normalizedKey) {
      return true;
    }

    const parts = normalizedKey.split('.');
    const chainKeys: string[] = [];
    let prefix = '';
    for (let i = 0; i < parts.length; i++) {
      prefix = i === 0 ? parts[i] : `${prefix}.${parts[i]}`;
      chainKeys.push(prefix);
    }

    const rows = await this.database
      .get<FarmModule>('farm_modules')
      .query(Q.where('key', Q.oneOf(chainKeys)))
      .fetch();

    return isVisible(
      normalizedKey,
      rows.map((r) => ({ key: r.key, enabled: r.enabled })),
    );
  }

  /**
   * Writes the on/off row for `key` and, when a SyncApi is wired in, fires a
   * best-effort propagation to the server. The local row is the source of
   * truth for the navigation decision (ADR-0019 sec. 4, Art. 9): a failed
   * network call leaves the operator's choice intact, and the next pull
   * will reconcile with whatever the panel eventually decides.
   */
  async setEnabled(
    key: ModuleKey,
    enabled: boolean,
    updatedBy: string = 'field-app',
  ): Promise<void> {
    const normalizedKey = normalizeModuleKey(key);
    const rows = await this.database
      .get<FarmModule>('farm_modules')
      .query(Q.where('key', normalizedKey))
      .fetch();

    if (rows.length === 0) {
      await this.database.write(async () => {
        await this.database.get<FarmModule>('farm_modules').create((row) => {
          row.key = normalizedKey;
          row.enabled = enabled;
          row.disabledReason = enabled ? '' : 'apagado desde el teléfono';
          row.updatedAt = Date.now();
          row.updatedBy = updatedBy;
        });
      });
    } else {
      const existing = rows[0];
      if (existing.enabled !== enabled) {
        await this.database.write(async () => {
          await existing.update((row) => {
            row.enabled = enabled;
            row.disabledReason = enabled ? '' : 'apagado desde el teléfono';
            row.updatedAt = Date.now();
            row.updatedBy = updatedBy;
          });
        });
      }
    }

    // Best-effort propagation. A failure here is logged but never blocks the
    // local toggle: the pull is the eventual source of truth, and the panel
    // can replay the operator's choice when the signal returns.
    if (this.api?.setFarmModuleEnabled) {
      try {
        await this.api.setFarmModuleEnabled(
          normalizedKey,
          enabled,
          enabled ? undefined : 'apagado desde el teléfono',
        );
      } catch {
        // The next pull wins. ADR-0019 sec. 1: the panel is the canonical writer
        // anyway, so the in-app toggle is only an accelerator.
      }
    }
  }
}
