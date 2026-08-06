import { newUuid } from '../src/services/identifiers';

/**
 * The client operation id is the idempotency key of the whole sync protocol: if two
 * different records ever share one, the server accepts the first and silently answers
 * "duplicate" to the second — a lost record that no error message ever mentions. So it
 * must come from the platform's cryptographic RNG, not from Math.random().
 */
describe('newUuid', () => {
  it('produces a syntactically valid RFC 4122 version 4 identifier', () => {
    const id = newUuid();

    expect(id).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
  });

  it('does not repeat across a large burst', () => {
    const ids = new Set(Array.from({ length: 5000 }, () => newUuid()));

    expect(ids.size).toBe(5000);
  });

  it('draws from the platform crypto module rather than Math.random', () => {
    const spy = jest.spyOn(Math, 'random');

    newUuid();

    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
