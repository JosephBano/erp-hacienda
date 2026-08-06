import { schemaMigrations, createTable, addColumns } from '@nozbe/watermelondb/Schema/migrations';

/**
 * Versioned local migrations (PLAN-FASE-3-4 sec.3.B).
 *
 * A phone in the field cannot be wiped and re-seeded to pick up a schema change: it may
 * be carrying a week of unsynced records. Every schema bump therefore needs a migration
 * step here, or WatermelonDB will refuse to open the database and the employee loses the
 * queue.
 */
export const migrations = schemaMigrations({
  migrations: [
    {
      toVersion: 2,
      steps: [
        createTable({
          name: 'species',
          columns: [
            { name: 'name', type: 'string' },
            { name: 'gestation_days', type: 'number', isOptional: true },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        createTable({
          name: 'breeds',
          columns: [
            { name: 'species_id', type: 'string', isIndexed: true },
            { name: 'name', type: 'string' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        createTable({
          name: 'animal_categories',
          columns: [
            { name: 'species_id', type: 'string', isIndexed: true },
            { name: 'name', type: 'string' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        createTable({
          name: 'withdrawal_periods',
          columns: [
            { name: 'animal_id', type: 'string', isIndexed: true },
            { name: 'event_id', type: 'string' },
            { name: 'target', type: 'string' },
            { name: 'starts_at', type: 'string' },
            { name: 'ends_at', type: 'string' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        createTable({
          name: 'milk_yields',
          columns: [
            { name: 'client_operation_id', type: 'string', isIndexed: true },
            { name: 'animal_id', type: 'string', isOptional: true },
            { name: 'group_id', type: 'string', isOptional: true },
            { name: 'shift', type: 'string' },
            { name: 'liters', type: 'number' },
            { name: 'date', type: 'string', isIndexed: true },
          ],
        }),
        createTable({
          name: 'sync_meta',
          columns: [
            { name: 'key', type: 'string', isIndexed: true },
            { name: 'value', type: 'string' },
          ],
        }),
        addColumns({
          table: 'sync_outbox',
          columns: [
            { name: 'result_ref', type: 'string', isOptional: true },
            { name: 'attempts', type: 'number' },
            { name: 'queued_at', type: 'number' },
          ],
        }),
        addColumns({
          table: 'animals',
          columns: [
            { name: 'server_created_at', type: 'number' },
            { name: 'server_updated_at', type: 'number', isOptional: true },
          ],
        }),
      ],
    },
    {
      toVersion: 3,
      steps: [
        addColumns({
          table: 'animals',
          columns: [{ name: 'last_edited_at', type: 'number', isOptional: true }],
        }),
      ],
    },
    {
      toVersion: 4,
      steps: [
        // The milking screen now filters candidates by their species' `is_milkable` flag
        // (Art. 8: species-level capability lives in the database). Defaulting the column
        // to false on existing rows means an animal on a species that was unknown before
        // this sync simply doesn't show up in Ordeño until the operator opts the species
        // in from the admin-web panel — fail-closed, no surprise registrations.
        addColumns({
          table: 'species',
          columns: [{ name: 'is_milkable', type: 'boolean' }],
        }),
      ],
    },
    {
      toVersion: 5,
      steps: [
        // ADR-0019: per-module visibility flag. The table starts empty — defaulting
        // visibility to "shown" for any module without a row keeps the upgrade silent.
        createTable({
          name: 'farm_modules',
          columns: [
            { name: 'key', type: 'string', isIndexed: true },
            { name: 'enabled', type: 'boolean' },
            { name: 'disabled_reason', type: 'string', isOptional: true },
            { name: 'updated_at', type: 'number' },
            { name: 'updated_by', type: 'string' },
          ],
        }),
      ],
    },
    {
      toVersion: 6,
      steps: [
        // ADR-0015: a group by headcount knows how many members it has, not which ones.
        // Existing local rows default to '' until the next pull overwrites them with the
        // real value — the same fail-silent-then-corrected pattern toVersion 4 used for
        // is_milkable.
        addColumns({
          table: 'animal_groups',
          columns: [{ name: 'tracking_mode', type: 'string' }],
        }),
      ],
    },
  ],
});
