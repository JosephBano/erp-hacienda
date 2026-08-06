/**
 * Navigation identifiers for the field-app tab state.
 *
 * Lives in its own module so the `<HomeScreen>` menu and the `App` shell both speak the
 * same vocabulary. Adding a destination is a touch here, in `HomeScreen.tsx`, and in the
 * `tab === X` branch of `App.tsx` — three lines, not three files to grep.
 */
export type TabKey = 'home' | 'milking' | 'events' | 'birth' | 'editAnimal' | 'sync';
