import { schema } from '../src/database/schema';

describe('WatermelonDB Schema Tests', () => {
  test('schema version and required tables are registered', () => {
    expect(schema.version).toBe(1);
    const tableNames = Object.keys(schema.tables);

    expect(tableNames).toContain('animals');
    expect(tableNames).toContain('animal_identifiers');
    expect(tableNames).toContain('animal_groups');
    expect(tableNames).toContain('group_memberships');
    expect(tableNames).toContain('inventory_items');
    expect(tableNames).toContain('sync_outbox');
  });

  test('animals table has required columns', () => {
    const animalsTable = schema.tables['animals'];
    const columnNames = Object.keys(animalsTable.columns);

    expect(columnNames).toContain('server_id');
    expect(columnNames).toContain('sex');
    expect(columnNames).toContain('species_id');
    expect(columnNames).toContain('is_deleted');
    expect(columnNames).toContain('created_at');
  });
});
