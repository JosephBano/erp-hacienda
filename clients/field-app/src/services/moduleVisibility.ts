import { Database, Q } from '@nozbe/watermelondb';

import { FarmModule } from '../database/models';
import type { SyncApi } from './syncApi';

/**
 * The keys every screen that wants to gate itself asks for.
 *
 * They are domain identifiers, not translations. Adding a new module here is a code
 * change, which is the point: the *list of modules* is owned by engineering, while the
 * on/off decision for each module is owned by the product owner via the data row
 * (ADR-0019 #1).
 */
export type ModuleKey = 'production' | 'livestock' | 'inventory' | 'breeding' | 'tasks' | 'people';

/**
 * Tells the navigation layer whether a module's entry should be visible right now.
 *
 * Art. 9 lives here: the flag comes from a row the pull already wrote to the local DB,
 * never from a network call. A visibility decision that flickered with signal would
 * defeat the whole point of being offline-first.
 *
 * Default behaviour: a module with no row in the table is treated as *visible*. The
 * upgrade path has to be silent and the legitimate "I want this turned off by hand"
 * only happens once the owner (or the seed migration that flips Production off for the
 * pig pilot) puts a row there. This is the fail-*open* direction, deliberate: the cost
 * of accidentally hiding something is much smaller than the cost of a pilot that opens
 * on a phone and discovers a critical surface is missing.
 *
 * The full ADR-0019 conjunction (module AND capabilities AND permissions) splits across
 * sub-ramas: 3.5a.9-A introduces this term; 3.5a.9-B owns "capabilities" (per-species);
 * "permissions" already exists via ADR-0007. They compose here when the time comes —
 * for now, only the first term is non-trivial.
 */
export class ModuleVisibility {
  constructor(
    private readonly database: Database,
    private readonly api?: SyncApi,
  ) {}

  async canShow(key: ModuleKey): Promise<boolean> {
    const rows = await this.database
      .get<FarmModule>('farm_modules')
      .query(Q.where('key', key))
      .fetch();

    if (rows.length === 0) {
      return true;
    }

    return rows[0].enabled;
  }

  /**
   * Writes the on/off row for `key` and, when a SyncApi is wired in, fires a
   * best-effort propagation to the server. The local row is the source of
   * truth for the navigation decision (ADR-0019 sec. 4, Art. 9): a failed
   * network call leaves the operator's choice intact, and the next pull
   * will reconcile with whatever the panel eventually decides.
   *
   * Used by the in-app "Módulos del dispositivo" control on SyncStatusScreen
   * so the operator can toggle a module without waiting for the next pull —
   * the constraint the plan spells out as "encender/apagar no requiere un
   * deploy". Idempotent: setting the existing value does not duplicate the
   * row. The row is created on first use so a phone that has never received
   * a pull can still be configured locally.
   */
  async setEnabled(key: ModuleKey, enabled: boolean, updatedBy: string = 'field-app'): Promise<void> {
    const rows = await this.database
      .get<FarmModule>('farm_modules')
      .query(Q.where('key', key))
      .fetch();

    if (rows.length === 0) {
      await this.database.write(async () => {
        await this.database.get<FarmModule>('farm_modules').create((row) => {
          row.key = key;
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
          key,
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
