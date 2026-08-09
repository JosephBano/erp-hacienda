import { appSchema, tableSchema } from '@nozbe/watermelondb';

/**
 * Local schema of the field app.
 *
 * Two families of tables live here. The *mirror* tables (`animals`, `species`,
 * `withdrawal_periods`, …) hold what the server sent down through the pull, keyed by the
 * server's own UUID so a re-sync updates rather than duplicates. The *local* tables
 * (`sync_outbox`, `milk_yields`, `sync_meta`) hold what this phone produced and the server
 * has not necessarily seen yet.
 *
 * Every column the pull delivers is stored, including `is_deleted`: a tombstone has to be
 * representable locally, otherwise a record deleted on the server would live on in the
 * employee's list forever.
 */
export const SCHEMA_VERSION = 9;

export const schema = appSchema({
  version: SCHEMA_VERSION,
  tables: [
    tableSchema({
      name: 'animals',
      columns: [
        { name: 'sex', type: 'string' },
        { name: 'birth_date', type: 'string', isOptional: true },
        { name: 'species_id', type: 'string', isIndexed: true },
        { name: 'breed_id', type: 'string', isOptional: true },
        { name: 'category_id', type: 'string', isOptional: true },
        { name: 'mother_id', type: 'string', isOptional: true },
        { name: 'father_animal_id', type: 'string', isOptional: true },
        { name: 'father_straw_id', type: 'string', isOptional: true },
        { name: 'is_deleted', type: 'boolean' },
        { name: 'server_created_at', type: 'number' },
        { name: 'server_updated_at', type: 'number', isOptional: true },
        // The LWW baseline (ADR-0008), distinct from server_updated_at (processing
        // time): the moment the field declares an edit happened. Echoed back as
        // knownUpdatedAt on the next updateAnimal push so the server can tell whether
        // another device's edit landed after this one last saw the row.
        { name: 'last_edited_at', type: 'number', isOptional: true },
      ],
    }),
    tableSchema({
      name: 'animal_identifiers',
      columns: [
        { name: 'animal_id', type: 'string', isIndexed: true },
        { name: 'type', type: 'string' },
        { name: 'value', type: 'string', isIndexed: true },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'animal_groups',
      columns: [
        { name: 'name', type: 'string' },
        { name: 'description', type: 'string', isOptional: true },
        { name: 'species_id', type: 'string', isOptional: true },
        { name: 'is_active', type: 'boolean' },
        // ADR-0015: Individual | Headcount. Evaluated locally (Art. 9) so the app can
        // tell a field screen "this lot doesn't know which animal is which" without
        // a round trip.
        { name: 'tracking_mode', type: 'string' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'group_memberships',
      columns: [
        { name: 'animal_id', type: 'string', isIndexed: true },
        { name: 'group_id', type: 'string', isIndexed: true },
        { name: 'joined_at', type: 'string' },
        { name: 'left_at', type: 'string', isOptional: true },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'species',
      columns: [
        { name: 'name', type: 'string' },
        { name: 'gestation_days', type: 'number', isOptional: true },
        { name: 'is_milkable', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'breeds',
      columns: [
        { name: 'species_id', type: 'string', isIndexed: true },
        { name: 'name', type: 'string' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'animal_categories',
      columns: [
        { name: 'species_id', type: 'string', isIndexed: true },
        { name: 'name', type: 'string' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'inventory_items',
      columns: [
        { name: 'name', type: 'string' },
        { name: 'category', type: 'string' },
        { name: 'unit', type: 'string' },
        { name: 'description', type: 'string', isOptional: true },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    // Art. 19 blocking rule, evaluated locally: the milking screen must be able to say
    // "this cow's milk is not sellable" with no signal at all.
    tableSchema({
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
    tableSchema({
      name: 'sync_outbox',
      columns: [
        { name: 'client_operation_id', type: 'string', isIndexed: true },
        { name: 'operation_type', type: 'string' },
        { name: 'occurred_at', type: 'string' },
        { name: 'payload_json', type: 'string' },
        { name: 'status', type: 'string', isIndexed: true },
        { name: 'error_details', type: 'string', isOptional: true },
        { name: 'result_ref', type: 'string', isOptional: true },
        { name: 'attempts', type: 'number' },
        { name: 'queued_at', type: 'number' },
      ],
    }),
    // What this phone recorded today, kept locally so the day's summary and the
    // "correct it before it syncs" rule work without a round trip.
    tableSchema({
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
    tableSchema({
      name: 'sync_meta',
      columns: [
        { name: 'key', type: 'string', isIndexed: true },
        { name: 'value', type: 'string' },
      ],
    }),
    // ADR-0019: a per-module on/off flag owned by the product owner, not derived from any
    // other data. Default visibility for a module whose row is absent is "shown" so a
    // fresh install does not silently lose a surface; the seed that flips Production off
    // for the pig pilot comes through the pull, not a migration.
    // 3.5a.3: the mortality causes catalog (Art. 8), so "baja con causa" offers the list
    // offline instead of blocking on a round trip the field may not have.
    tableSchema({
      name: 'mortality_causes',
      columns: [
        { name: 'name', type: 'string' },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'farm_modules',
      columns: [
        { name: 'key', type: 'string', isIndexed: true },
        { name: 'enabled', type: 'boolean' },
        { name: 'disabled_reason', type: 'string', isOptional: true },
        { name: 'updated_at', type: 'number' },
        { name: 'updated_by', type: 'string' },
      ],
    }),
    // 3.5a.2-C (ADR-0016 + 3.5a.2-A): catalogs required by VaccinateScreen and
    // TreatScreen. Mirrored so the field can build a structured treatment
    // payload offline without a round trip to the server (Art. 9).
    tableSchema({
      name: 'administration_routes',
      columns: [
        { name: 'key', type: 'string', isIndexed: true },
        { name: 'label_es', type: 'string' },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'treatment_reasons',
      columns: [
        { name: 'key', type: 'string', isIndexed: true },
        { name: 'label_es', type: 'string' },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    // 3.5a.6 (ADR-0022): plausibility ranges for offline validation of
    // weights and milk volumes. The validator (see plausibilityService) is
    // fail-open: a missing row for the (species, category, magnitude)
    // combination returns pass, not block.
    tableSchema({
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
    // 3.5a.1 (ADR-0015) + BACKLOG "AnimalEvent grupal aún no viaja en el pull": the
    // event history now travels on the pull, which is what 3.5a.7's lot record
    // ("última vacunación, alimento del período") needs. One table carries both
    // animal- and group-subject events — `animal_id` XOR `group_id`, mirroring the
    // DB CHECK the server already enforces (ADR-0015 sec.2). Both columns stay
    // optional on purpose: forcing a fake `animal_id` on a group event to keep the
    // row uniform would be exactly the synthetic data ADR-0015 exists to prevent.
    tableSchema({
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
});
