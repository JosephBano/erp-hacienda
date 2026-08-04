import * as Crypto from 'expo-crypto';

/**
 * Identifiers minted on the phone, with no signal (Art. 3).
 *
 * These are not cosmetic ids. A `clientOperationId` is the idempotency key the server
 * uses to decide whether an incoming operation is new or a replay, and an animal id
 * created here becomes that animal's permanent identity in the herd. Both must come from
 * the platform's cryptographic generator: `Math.random()` is seeded per JS context, so two
 * phones that start from a fresh install can walk the same sequence and mint the same id.
 */
export function newUuid(): string {
  return Crypto.randomUUID();
}
