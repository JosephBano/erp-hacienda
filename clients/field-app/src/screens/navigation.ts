/**
 * Navigation identifiers for the field-app tab state.
 *
 * Lives in its own module so the activities hub and the `App` shell both speak the
 * same vocabulary. Adding a destination is a touch here, in `App.tsx`, and in the
 * screen the new tab key points at — three lines, not three files to grep.
 *
 * 3.5a.9-B introduced two flow destinations that are screens of their own:
 *  - 'animal-subject' opens the picker/activities for a single animal.
 *  - 'today' opens the on-phone accountability view.
 *
 * 3.5a.7 adds the lot-subject pair, mirroring the animal-subject shape:
 *  - 'lot-subject' opens the picker/activities for a single headcount lot.
 *  - 'lot-events' opens the form for the chosen lot + activity (ADR-0015).
 */
export type TabKey =
  | 'home'
  | 'animal-subject'
  | 'lot-subject'
  | 'lot-events'
  | 'today'
  | 'milking'
  | 'events'
  | 'birth'
  | 'editAnimal'
  | 'sync';
