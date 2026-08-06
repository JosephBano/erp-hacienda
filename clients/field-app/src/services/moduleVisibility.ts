import { Database, Q } from '@nozbe/watermelondb';

import { FarmModule } from '../database/models';

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
  constructor(private readonly database: Database) {}

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
}
