import { appSchema, tableSchema } from '@nozbe/watermelondb';

export const schema = appSchema({
  version: 1,
  tables: [
    tableSchema({
      name: 'animals',
      columns: [
        { name: 'server_id', type: 'string', isOptional: true },
        { name: 'sex', type: 'string' },
        { name: 'birth_date', type: 'string', isOptional: true },
        { name: 'species_id', type: 'string' },
        { name: 'breed_id', type: 'string', isOptional: true },
        { name: 'category_id', type: 'string', isOptional: true },
        { name: 'mother_id', type: 'string', isOptional: true },
        { name: 'father_animal_id', type: 'string', isOptional: true },
        { name: 'father_straw_id', type: 'string', isOptional: true },
        { name: 'is_deleted', type: 'boolean' },
        { name: 'created_at', type: 'number' },
        { name: 'updated_at', type: 'number' },
      ],
    }),
    tableSchema({
      name: 'animal_identifiers',
      columns: [
        { name: 'server_id', type: 'string', isOptional: true },
        { name: 'animal_id', type: 'string' },
        { name: 'type', type: 'string' },
        { name: 'value', type: 'string' },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'animal_groups',
      columns: [
        { name: 'server_id', type: 'string', isOptional: true },
        { name: 'name', type: 'string' },
        { name: 'description', type: 'string', isOptional: true },
        { name: 'species_id', type: 'string', isOptional: true },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'group_memberships',
      columns: [
        { name: 'server_id', type: 'string', isOptional: true },
        { name: 'animal_id', type: 'string' },
        { name: 'group_id', type: 'string' },
        { name: 'joined_at', type: 'string' },
        { name: 'left_at', type: 'string', isOptional: true },
        { name: 'is_active', type: 'boolean' },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'inventory_items',
      columns: [
        { name: 'server_id', type: 'string', isOptional: true },
        { name: 'name', type: 'string' },
        { name: 'category', type: 'string' },
        { name: 'unit', type: 'string' },
        { name: 'description', type: 'string', isOptional: true },
        { name: 'is_deleted', type: 'boolean' },
      ],
    }),
    tableSchema({
      name: 'sync_outbox',
      columns: [
        { name: 'client_operation_id', type: 'string' },
        { name: 'operation_type', type: 'string' },
        { name: 'occurred_at', type: 'string' },
        { name: 'payload_json', type: 'string' },
        { name: 'status', type: 'string' }, // 'pending' | 'synced' | 'rejected'
        { name: 'error_details', type: 'string', isOptional: true },
        { name: 'created_at', type: 'number' },
      ],
    }),
  ],
});
