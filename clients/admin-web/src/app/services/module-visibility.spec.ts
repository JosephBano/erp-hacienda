import { describe, expect, it } from 'vitest';
import {
  type ModuleRow,
  isVisible,
  normalizeModuleKey,
} from './module-visibility';

describe('module-visibility', () => {
  describe('normalizeModuleKey', () => {
    it('normalizes uppercase and trims whitespace', () => {
      expect(normalizeModuleKey('Inventory.X ')).toBe('inventory.x');
      expect(normalizeModuleKey('  Production  ')).toBe('production');
    });
  });

  describe('isVisible shared visibility table (tasks.md / spec.md sec. 5)', () => {
    it('returns visible when no rows exist for inventory.transformations', () => {
      const rows: ModuleRow[] = [];
      expect(isVisible('inventory.transformations', rows)).toBe(true);
    });

    it('returns hidden for inventory.transformations when inventory is off', () => {
      const rows: ModuleRow[] = [{ key: 'inventory', enabled: false }];
      expect(isVisible('inventory.transformations', rows)).toBe(false);
    });

    it('returns hidden for inventory.transformations when inventory is on but inventory.transformations is off', () => {
      const rows: ModuleRow[] = [
        { key: 'inventory', enabled: true },
        { key: 'inventory.transformations', enabled: false },
      ];
      expect(isVisible('inventory.transformations', rows)).toBe(false);
    });

    it('returns visible for inventory when inventory is on but inventory.transformations is off', () => {
      const rows: ModuleRow[] = [
        { key: 'inventory', enabled: true },
        { key: 'inventory.transformations', enabled: false },
      ];
      expect(isVisible('inventory', rows)).toBe(true);
    });

    it('returns hidden for inventory.transformations when inventory is off even if inventory.transformations is on', () => {
      const rows: ModuleRow[] = [
        { key: 'inventory', enabled: false },
        { key: 'inventory.transformations', enabled: true },
      ];
      expect(isVisible('inventory.transformations', rows)).toBe(false);
    });

    it('returns visible for inventory.usages (without row) when inventory is on', () => {
      const rows: ModuleRow[] = [{ key: 'inventory', enabled: true }];
      expect(isVisible('inventory.usages', rows)).toBe(true);
    });
  });
});
