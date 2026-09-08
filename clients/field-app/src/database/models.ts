import { Model } from '@nozbe/watermelondb';
import { field, text } from '@nozbe/watermelondb/decorators';

/**
 * WatermelonDB models.
 *
 * Mirror records reuse the server's UUID as the local primary key. That is the whole
 * reason a second pull of the same row updates it instead of creating a twin, and it is
 * what lets an operation queued offline reference an animal by the same id the server
 * knows (Art. 3).
 */

export class Animal extends Model {
  static table = 'animals';

  @text('sex') declare sex: string;
  @text('birth_date') birthDate?: string;
  @text('species_id') declare speciesId: string;
  @text('breed_id') breedId?: string;
  @text('category_id') categoryId?: string;
  @text('mother_id') motherId?: string;
  @text('father_animal_id') fatherAnimalId?: string;
  @text('father_straw_id') fatherStrawId?: string;
  @field('is_deleted') declare isDeleted: boolean;
  @field('server_created_at') declare serverCreatedAt: number;
  @field('server_updated_at') serverUpdatedAt?: number;
  @field('last_edited_at') lastEditedAt?: number;
  @text('disposed_at') disposedAt?: string;
}

export class AnimalIdentifier extends Model {
  static table = 'animal_identifiers';

  @text('animal_id') declare animalId: string;
  @text('type') declare type: string;
  @text('value') declare value: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

export class AnimalGroup extends Model {
  static table = 'animal_groups';

  @text('name') declare name: string;
  @text('description') description?: string;
  @text('species_id') speciesId?: string;
  @field('is_active') declare isActive: boolean;
  @text('tracking_mode') declare trackingMode: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class GroupMembership extends Model {
  static table = 'group_memberships';

  @text('animal_id') declare animalId: string;
  @text('group_id') declare groupId: string;
  @text('joined_at') declare joinedAt: string;
  @text('left_at') leftAt?: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

export class Species extends Model {
  static table = 'species';

  @text('name') declare name: string;
  @field('gestation_days') gestationDays?: number;
  // Whether the field app should offer this species for milking registration. Art. 8:
  // per-species capability lives in the DB. No JS-side default — WatermelonDB forbids
  // default values on decorated fields, and the column itself has no SQL default,
  // so a species row created without an explicit value reads back as undefined here;
  // callers must coalesce to a fail-closed `false`.
  @field('is_milkable') isMilkable?: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

export class Breed extends Model {
  static table = 'breeds';

  @text('species_id') declare speciesId: string;
  @text('name') declare name: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class AnimalCategory extends Model {
  static table = 'animal_categories';

  @text('species_id') declare speciesId: string;
  @text('name') declare name: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class InventoryItem extends Model {
  static table = 'inventory_items';

  @text('name') declare name: string;
  @text('category') declare category: string;
  @text('unit') declare unit: string;
  @text('description') description?: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class WithdrawalPeriod extends Model {
  static table = 'withdrawal_periods';

  @text('animal_id') declare animalId: string;
  @text('event_id') declare eventId: string;
  @text('target') declare target: string;
  @text('starts_at') declare startsAt: string;
  @text('ends_at') declare endsAt: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class OutboxEntryModel extends Model {
  static table = 'sync_outbox';

  @text('client_operation_id') declare clientOperationId: string;
  @text('operation_type') declare operationType: string;
  @text('occurred_at') declare occurredAt: string;
  @text('payload_json') declare payloadJson: string;
  @text('status') declare status: string;
  @text('error_details') errorDetails?: string;
  @text('result_ref') resultRef?: string;
  @field('attempts') declare attempts: number;
  @field('queued_at') declare queuedAt: number;
}

export class MilkYield extends Model {
  static table = 'milk_yields';

  @text('client_operation_id') declare clientOperationId: string;
  @text('animal_id') animalId?: string;
  @text('group_id') groupId?: string;
  @text('shift') declare shift: string;
  @field('liters') declare liters: number;
  @text('date') declare date: string;
}

export class SyncMeta extends Model {
  static table = 'sync_meta';

  @text('key') declare key: string;
  @text('value') declare value: string;
}

export class FarmModule extends Model {
  static table = 'farm_modules';

  @text('key') declare key: string;
  @field('enabled') declare enabled: boolean;
  @text('disabled_reason') disabledReason?: string;
  @field('updated_at') declare updatedAt: number;
  @text('updated_by') declare updatedBy: string;
}

export class MortalityCause extends Model {
  static table = 'mortality_causes';

  @text('name') declare name: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

/**
 * Mirror of the server's `administration_routes` catalog (3.5a.2-A). The
 * field app needs the active list to build a structured treatment
 * payload — `routeId` is a foreign key the server will reject if the
 * local pick is stale.
 */
export class AdministrationRoute extends Model {
  static table = 'administration_routes';

  @text('key') declare key: string;
  @text('label_es') declare labelEs: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

/**
 * Mirror of the server's `treatment_reasons` catalog (3.5a.2-A). The
 * three values (`scheduled`, `curative`, `preventive`) are what separate
 * "vacuna de calendario" from "vacuna porque se enfermó" on the herd's
 * history.
 */
export class TreatmentReason extends Model {
  static table = 'treatment_reasons';

  @text('key') declare key: string;
  @text('label_es') declare labelEs: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

/**
 * Mirror of the server's `dose_kinds` catalog (3.5a.2-B: absolute / per_weight /
 * per_head). `VaccinateScreen` and `TreatScreen` (3.5a.2-C) resolve `doseKindId`
 * from here to build the `createTreatmentCourse` payload offline (Art. 9),
 * instead of hardcoding the seed's stable GUIDs client-side.
 */
export class DoseKind extends Model {
  static table = 'dose_kinds';

  @text('key') declare key: string;
  @text('label_es') declare labelEs: string;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

/**
 * Mirror of the server's `plausibility_ranges` catalog (3.5a.6, ADR-0022).
 * The four bounds classify a recorded value into pass/confirm/block. The
 * evaluator (`plausibilityService`) is fail-open: a missing row for the
 * (species, category, magnitude) combination returns pass, so a range
 * forgotten by the operator never prevents registering a real field datum.
 */
export class PlausibilityRange extends Model {
  static table = 'plausibility_ranges';

  @text('species_id') declare speciesId: string;
  @text('category_id') categoryId?: string;
  @text('magnitude') declare magnitude: string;
  @field('plausible_min') plausibleMin?: number;
  @field('plausible_max') plausibleMax?: number;
  @field('absolute_min') absoluteMin?: number;
  @field('absolute_max') absoluteMax?: number;
  @field('is_active') declare isActive: boolean;
  @field('is_deleted') declare isDeleted: boolean;
}

/**
 * Mirror of the server's `animal_events` history (3.5a.1, ADR-0015). Exactly one of
 * `animalId` / `groupId` is set, never both and never neither — the same XOR the
 * domain and the DB CHECK enforce server-side. Consumers (e.g. the lot record's
 * "última vacunación, alimento del período") must branch on which one is present
 * instead of assuming `animalId` is always populated.
 */
export class AnimalEvent extends Model {
  static table = 'animal_events';

  @text('animal_id') animalId?: string;
  @text('group_id') groupId?: string;
  @text('event_type') declare eventType: string;
  @text('occurred_at') declare occurredAt: string;
  @text('recorded_by') declare recordedBy: string;
  @text('recorded_by_id') recordedById?: string;
  @text('payload_json') declare payloadJson: string;
  @field('cost') cost?: number;
  @text('related_event_id') relatedEventId?: string;
  @field('affected_count') affectedCount?: number;
  @text('cause_id') causeId?: string;
  @text('route_id') routeId?: string;
  @text('reason') reason?: string;
  @text('batch_id') batchId?: string;
  @text('health_plan_item_id') healthPlanItemId?: string;
  @text('applied_by_user_id') appliedByUserId?: string;
  @field('is_deleted') declare isDeleted: boolean;
}

export class Pregnancy extends Model {
  static table = 'pregnancies';

  @text('dam_id') declare damId: string;
  @text('service_id') serviceId?: string;
  @text('status') declare status: string;
  @text('expected_birth_date') expectedBirthDate?: string;
  @field('is_deleted') declare isDeleted: boolean;
  @field('server_created_at') declare serverCreatedAt: number;
  @field('server_updated_at') serverUpdatedAt?: number;
}

export class BreedingService extends Model {
  static table = 'breeding_services';

  @text('dam_id') declare damId: string;
  @text('service_type') declare serviceType: string;
  @text('sire_animal_id') sireAnimalId?: string;
  @text('straw_id') strawId?: string;
  @field('is_deleted') declare isDeleted: boolean;
  @field('server_created_at') declare serverCreatedAt: number;
  @field('server_updated_at') serverUpdatedAt?: number;
}

export const modelClasses = [
  Animal,
  AnimalIdentifier,
  AnimalGroup,
  GroupMembership,
  Species,
  Breed,
  AnimalCategory,
  InventoryItem,
  WithdrawalPeriod,
  OutboxEntryModel,
  MilkYield,
  SyncMeta,
  FarmModule,
  MortalityCause,
  AdministrationRoute,
  TreatmentReason,
  DoseKind,
  PlausibilityRange,
  AnimalEvent,
  Pregnancy,
  BreedingService,
];

