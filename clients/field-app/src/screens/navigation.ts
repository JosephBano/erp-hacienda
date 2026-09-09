/**
 * Navigation identifiers for the field-app tab state.
 *
 * Feature 0010 (field-app-redesign) establishes the four canonical destinations:
 *  - 'home': Inicio (operations hub, quick search, recent work).
 *  - 'animals': Animales (individual animal catalog, tag search, animal record).
 *  - 'lots': Lotes (animal groups, headcount vs tagged, group events).
 *  - 'activity': Actividad (on-phone audit trail of registrations and sync status).
 *
 * Secondary and flow destinations:
 *  - 'animal-subject': alias for 'animals' (legacy compatibility).
 *  - 'lot-subject': alias for 'lots' (legacy compatibility).
 *  - 'today': alias for 'activity' (legacy compatibility).
 *  - 'lot-events': form for chosen lot + activity.
 *  - 'events': events screen (weight, move, disposal).
 *  - 'vaccinate': vaccination form.
 *  - 'treat': treatment form.
 *  - 'birth': birth assistant.
 *  - 'editAnimal': animal edit form.
 *  - 'sync': full synchronization and diagnostics screen.
 *  - 'milking': milking register (gated by module visibility).
 */
export type CanonicalTab = 'home' | 'animals' | 'lots' | 'activity';

export type TabKey =
  | CanonicalTab
  | 'animal-subject'
  | 'lot-subject'
  | 'lot-events'
  | 'today'
  | 'milking'
  | 'events'
  | 'vaccinate'
  | 'treat'
  | 'birth'
  | 'editAnimal'
  | 'sync';

export interface NavDestination {
  readonly key: CanonicalTab;
  readonly label: string;
  readonly symbol: string;
  readonly testID: string;
}

export const CANONICAL_DESTINATIONS: readonly NavDestination[] = [
  { key: 'home', label: 'Inicio', symbol: '🏠', testID: 'nav-home' },
  { key: 'animals', label: 'Animales', symbol: '🏷️', testID: 'nav-animals' },
  { key: 'lots', label: 'Lotes', symbol: '👥', testID: 'nav-lots' },
  { key: 'activity', label: 'Actividad', symbol: '📋', testID: 'nav-activity' },
] as const;

export function resolveCanonicalTab(tab: TabKey): CanonicalTab | null {
  if (tab === 'home') return 'home';
  if (tab === 'animals' || tab === 'animal-subject') return 'animals';
  if (tab === 'lots' || tab === 'lot-subject' || tab === 'lot-events') return 'lots';
  if (tab === 'activity' || tab === 'today') return 'activity';
  return null;
}
