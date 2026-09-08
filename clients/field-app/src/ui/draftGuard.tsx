import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';

/**
 * Lets a screen tell the shell "the employee has typed something here".
 *
 * The shell owns navigation (`App.tsx` renders one screen per tab key) but has
 * no way to see inside a screen's form state, so leaving a half-filled
 * treatment used to throw the entry away silently — the employee walks back to
 * the animal and types it again, in the rain. feature-0006 D3 forbids that:
 * "abandonarlo con cambios advierte antes de descartarlos".
 *
 * A context rather than a prop threaded through eleven screens: the shell has
 * to know about *any* screen being dirty, and a screen has to be able to say so
 * from wherever its form lives, including a wizard step three levels down.
 *
 * The flag is deliberately coarse — "something is unsaved here", not "which
 * field". The shell only ever asks one question with it.
 */
interface DraftGuard {
  /** Called by a screen whenever its "has unsaved input" answer changes. */
  markDirty: (dirty: boolean) => void;
}

const DraftGuardContext = createContext<DraftGuard>({ markDirty: () => undefined });

export function DraftGuardProvider({
  children,
  onDirtyChange,
}: {
  children: React.ReactNode;
  onDirtyChange: (dirty: boolean) => void;
}) {
  const value = useMemo(() => ({ markDirty: onDirtyChange }), [onDirtyChange]);
  return <DraftGuardContext.Provider value={value}>{children}</DraftGuardContext.Provider>;
}

/**
 * Reports whether this screen currently holds unsaved input, and clears the flag
 * when the screen unmounts — an unmounted screen cannot lose anything, and
 * leaving the flag set would make the *next* screen inherit a warning that is
 * not about it.
 */
export function useDraftFlag(dirty: boolean): void {
  const { markDirty } = useContext(DraftGuardContext);
  // The callback identity is stable in practice (App memoises it), but a screen
  // must not re-report on every render either, so the effect keys on the flag.
  useEffect(() => {
    markDirty(dirty);
  }, [dirty, markDirty]);

  useEffect(
    () => () => {
      markDirty(false);
    },
    [markDirty],
  );
}

/**
 * The shell side: tracks whether the screen on top has unsaved input, and gates
 * a navigation action behind a confirmation when it does.
 */
export function useDraftGate() {
  const [dirty, setDirty] = useState(false);
  // A ref as well as state, so `request` reads the current answer synchronously
  // instead of whatever React had applied by the time the tap arrived.
  const dirtyRef = useRef(false);
  const [pending, setPending] = useState<{ run: () => void } | null>(null);

  const markDirty = useCallback((next: boolean) => {
    dirtyRef.current = next;
    setDirty(next);
  }, []);

  /** Runs `action` now, or parks it behind a confirmation if there is unsaved input. */
  const request = useCallback((action: () => void) => {
    if (!dirtyRef.current) {
      action();
      return;
    }
    setPending({ run: action });
  }, []);

  const discard = useCallback(() => {
    setPending((current) => {
      current?.run();
      return null;
    });
    markDirty(false);
  }, [markDirty]);

  const keep = useCallback(() => setPending(null), []);

  return { dirty, markDirty, request, discard, keep, isAsking: pending !== null };
}
