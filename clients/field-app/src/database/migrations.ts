import { schemaMigrations, createTable, addColumns } from '@nozbe/watermelondb/Schema/migrations';

/**
 * Versioned local migrations (docs/spec/plan-0001-fase-3/spec.md sec.3.B).
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
    {
      toVersion: 7,
      steps: [
        // 3.5a.3: mortality causes catalog, pulled down so the disposal form works
        // offline the same way the medication and lot pickers already do.
        createTable({
          name: 'mortality_causes',
          columns: [
            { name: 'name', type: 'string' },
            { name: 'is_active', type: 'boolean' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
      ],
    },
    {
      toVersion: 8,
      steps: [
        // 3.5a.2-C (ADR-0016 + 3.5a.2-A): catalogs that the structured
        // treatment payload needs. Mirrored locally so the field app can
        // build the payload without a round-trip (Art. 9).
        createTable({
          name: 'administration_routes',
          columns: [
            { name: 'key', type: 'string', isIndexed: true },
            { name: 'label_es', type: 'string' },
            { name: 'is_active', type: 'boolean' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        createTable({
          name: 'treatment_reasons',
          columns: [
            { name: 'key', type: 'string', isIndexed: true },
            { name: 'label_es', type: 'string' },
            { name: 'is_active', type: 'boolean' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
        // 3.5a.6 (ADR-0022): plausibility ranges for offline validation.
        createTable({
          name: 'plausibility_ranges',
          columns: [
            { name: 'species_id', type: 'string', isIndexed: true },
            { name: 'category_id', type: 'string', isOptional: true, isIndexed: true },
            { name: 'magnitude', type: 'string', isIndexed: true },
            { name: 'plausible_min', type: 'number', isOptional: true },
            { name: 'plausible_max', type: 'number', isOptional: true },
            { name: 'absolute_min', type: 'number', isOptional: true },
            { name: 'absolute_max', type: 'number', isOptional: true },
            { name: 'is_active', type: 'boolean' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
      ],
    },
    {
      toVersion: 9,
      steps: [
        // 3.5a.1 (ADR-0015) + BACKLOG "AnimalEvent grupal aún no viaja en el
        // pull": the event history (individual and group-subject) now travels
        // on the pull, which is what 3.5a.7's lot record needs. A phone in
        // the field cannot be wiped to pick this up (docs/spec/plan-0001-fase-3/spec.md sec.3.B),
        // so this is a real migration, not a fresh install requirement.
        createTable({
          name: 'animal_events',
          columns: [
            { name: 'animal_id', type: 'string', isOptional: true, isIndexed: true },
            { name: 'group_id', type: 'string', isOptional: true, isIndexed: true },
            { name: 'event_type', type: 'string', isIndexed: true },
            { name: 'occurred_at', type: 'string', isIndexed: true },
            { name: 'recorded_by', type: 'string' },
            { name: 'recorded_by_id', type: 'string', isOptional: true },
            { name: 'payload_json', type: 'string' },
            { name: 'cost', type: 'number', isOptional: true },
            { name: 'related_event_id', type: 'string', isOptional: true },
            { name: 'affected_count', type: 'number', isOptional: true },
            { name: 'cause_id', type: 'string', isOptional: true },
            { name: 'route_id', type: 'string', isOptional: true },
            { name: 'reason', type: 'string', isOptional: true },
            { name: 'batch_id', type: 'string', isOptional: true },
            { name: 'health_plan_item_id', type: 'string', isOptional: true },
            { name: 'applied_by_user_id', type: 'string', isOptional: true },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
      ],
    },
    {
      toVersion: 10,
      steps: [
        // 3.5a.2-C: dose-form catalog, mirrored so VaccinateScreen/TreatScreen
        // can resolve a DoseKindId offline for createTreatmentCourse (Art. 9).
        createTable({
          name: 'dose_kinds',
          columns: [
            { name: 'key', type: 'string', isIndexed: true },
            { name: 'label_es', type: 'string' },
            { name: 'is_active', type: 'boolean' },
            { name: 'is_deleted', type: 'boolean' },
          ],
        }),
      ],
    },
    {
      toVersion: 11,
      steps: [
        createTable({
          name: 'pregnancies',
          columns: [
            { name: 'dam_id', type: 'string', isIndexed: true },
            { name: 'service_id', type: 'string', isOptional: true },
            { name: 'status', type: 'string', isIndexed: true },
            { name: 'expected_birth_date', type: 'string', isOptional: true },
            { name: 'is_deleted', type: 'boolean' },
            { name: 'server_created_at', type: 'number' },
            { name: 'server_updated_at', type: 'number', isOptional: true },
          ],
        }),
        createTable({
          name: 'breeding_services',
          columns: [
            { name: 'dam_id', type: 'string', isIndexed: true },
            { name: 'service_type', type: 'string' },
            { name: 'sire_animal_id', type: 'string', isOptional: true },
            { name: 'straw_id', type: 'string', isOptional: true },
            { name: 'is_deleted', type: 'boolean' },
            { name: 'server_created_at', type: 'number' },
            { name: 'server_updated_at', type: 'number', isOptional: true },
          ],
        }),
      ],
    },
    {
      toVersion: 12,
      steps: [
        // feature-0005: carry the animal disposal state to the field app (D1, D5)
        // so candidate filters and retrospective validations can tell when an animal
        // left the herd, without reducing the historical record.
        addColumns({
          table: 'animals',
          columns: [{ name: 'disposed_at', type: 'string', isOptional: true }],
        }),
      ],
    },
  ],
});

