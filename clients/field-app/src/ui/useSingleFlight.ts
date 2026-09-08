import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Runs a recording operation at most once at a time, refusing re-entry
 * *synchronously*.
 *
 * A `busy` state cannot do this on its own, and every confirm path in this app
 * used to try. React schedules state updates rather than applying them, so two
 * taps delivered in the same tick both read `busy === false` and both go
 * through. Worse, the plausibility paths (`MilkingScreen.record`,
 * `TreatScreen.confirm`) `await` the local range check *before* flipping the
 * flag, which leaves a real multi-millisecond window with the button still
 * enabled — a gloved double tap lands squarely inside it.
 *
 * The result is two outbox entries for one intention. History is immutable
 * (regla dura 1), so that duplicate is not a row someone deletes later: it is a
 * correction event an employee has to file, about an animal that was treated
 * once. That is what feature-0006 D2 forbids — "confirmar repetidamente
 * mientras se guarda produce una sola operación local".
 *
 * The latch is a `ref` because a ref is written synchronously: the second tap
 * reads what the first tap wrote, in the same tick, before any `await` has had
 * a chance to yield. `busy` exists only to drive the spinner — the phone must
 * still show that a save is in flight, which is the other half of D2.
 *
 * The operation keeps its own error handling. Nothing is swallowed here: a
 * rejection propagates to the caller exactly as it did before, and the latch is
 * released either way.
 */
export function useSingleFlight(): {
  /** True while an operation is in flight. Feed it to `BigButton`'s `busy`. */
  busy: boolean;
  /** Runs `operation` unless one is already running, in which case it is dropped. */
  runOnce: (operation: () => Promise<void>) => Promise<void>;
} {
  const inFlight = useRef(false);
  const mounted = useRef(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const runOnce = useCallback(async (operation: () => Promise<void>) => {
    // Synchronous guard. Everything above the first `await` below runs in the
    // caller's tick, which is the whole point.
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true);

    try {
      await operation();
    } finally {
      inFlight.current = false;
      // A screen that navigates away as part of the operation (BirthScreen goes
      // home on success) is already unmounted by the time we get here; setting
      // state then is a no-op React warns about.
      if (mounted.current) setBusy(false);
    }
  }, []);

  return { busy, runOnce };
}
