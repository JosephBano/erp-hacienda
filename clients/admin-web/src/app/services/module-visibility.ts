export interface ModuleRow {
  key: string;
  enabled: boolean;
}

/**
 * Normalizes a module or submodule key to lowercase and trims surrounding whitespace.
 * Example: "Inventory.X " -> "inventory.x".
 */
export function normalizeModuleKey(raw: string): string {
  return raw.trim().toLowerCase();
}

/**
 * Evaluates whether a module or submodule is visible based on the key chain.
 *
 * isVisible("a.b.c") = on("a") ∧ on("a.b") ∧ on("a.b.c")
 * on(k) = fila(k)?.enabled ?? true
 *
 * A key with no row is treated as enabled (fail-open, S2).
 * Visibility is the conjunction of the entire ancestor chain down to the node itself (ADR-0042).
 */
export function isVisible(key: string, rows: readonly ModuleRow[]): boolean {
  const normalizedTarget = normalizeModuleKey(key);
  if (!normalizedTarget) {
    return true;
  }

  const rowMap = new Map<string, boolean>();
  for (const row of rows) {
    rowMap.set(normalizeModuleKey(row.key), row.enabled);
  }

  const segments = normalizedTarget.split('.');
  let currentKey = '';

  for (let i = 0; i < segments.length; i++) {
    currentKey = i === 0 ? segments[i] : `${currentKey}.${segments[i]}`;
    const enabled = rowMap.get(currentKey);
    if (enabled === false) {
      return false;
    }
  }

  return true;
}
