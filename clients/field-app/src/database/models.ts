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
];
