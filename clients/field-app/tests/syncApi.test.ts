import { AuthenticationExpiredError, HttpSyncApi } from '../src/services/syncApi';

/**
 * Scenario 9 of PLAN-FASE-3-4 sec.2.2: the JWT expires mid-sync (a batch push can easily
 * outlast a short-lived token on a slow rural connection). The retry must use the
 * *refreshed* token and must not duplicate anything — duplication safety here comes for
 * free from the server's clientOperationId idempotency (proven server-side), so what this
 * suite actually has to prove is narrower and client-only: exactly one retry, with the new
 * token, and a clean failure when refresh itself fails.
 */
describe('HttpSyncApi', () => {
  const baseUrl = 'https://hato.test';
  let tokens: string[];
  let refreshCalls: number;

  beforeEach(() => {
    tokens = ['expired-token'];
    refreshCalls = 0;
  });

  function makeApi(refreshSucceeds: boolean) {
    return new HttpSyncApi({
      baseUrl,
      deviceId: 'device-1',
      getToken: () => tokens[tokens.length - 1],
      refreshToken: async () => {
        refreshCalls += 1;
        if (refreshSucceeds) {
          tokens.push('fresh-token');
        }
        return refreshSucceeds;
      },
    });
  }

  it('retries once with the refreshed token after a 401 and succeeds', async () => {
    const authHeadersSeen: string[] = [];

    global.fetch = jest.fn(async (_url, init: any) => {
      authHeadersSeen.push(init.headers.Authorization);

      if (authHeadersSeen.length === 1) {
        return { ok: false, status: 401, text: async () => 'expired' } as Response;
      }

      return {
        ok: true,
        status: 200,
        json: async () => ({ processedCount: 1, results: [] }),
      } as Response;
    }) as unknown as typeof fetch;

    const api = makeApi(true);
    const result = await api.push([
      { clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: new Date().toISOString(), payload: {} },
    ]);

    expect(result.processedCount).toBe(1);
    expect(refreshCalls).toBe(1);
    expect(authHeadersSeen).toEqual(['Bearer expired-token', 'Bearer fresh-token']);
  });

  it('does not retry a second time if the refreshed token also gets a 401', async () => {
    let callCount = 0;

    global.fetch = jest.fn(async () => {
      callCount += 1;
      return { ok: false, status: 401, text: async () => 'still expired' } as Response;
    }) as unknown as typeof fetch;

    const api = makeApi(true);

    await expect(
      api.push([{ clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: new Date().toISOString(), payload: {} }])
    ).rejects.toThrow(AuthenticationExpiredError);

    // One original attempt, one retry with the refreshed token — never an unbounded loop.
    expect(callCount).toBe(2);
  });

  it('fails clearly when the refresh itself is refused, without ever retrying the request', async () => {
    let callCount = 0;

    global.fetch = jest.fn(async () => {
      callCount += 1;
      return { ok: false, status: 401, text: async () => 'expired' } as Response;
    }) as unknown as typeof fetch;

    const api = makeApi(false);

    await expect(
      api.push([{ clientOperationId: 'op-1', operationType: 'recordMilking', occurredAt: new Date().toISOString(), payload: {} }])
    ).rejects.toThrow(AuthenticationExpiredError);

    expect(refreshCalls).toBe(1);
    expect(callCount).toBe(1);
  });

  it('replays the exact same clientOperationId on retry, which is what lets the server de-duplicate it', async () => {
    const bodiesSeen: string[] = [];

    global.fetch = jest.fn(async (_url, init: any) => {
      bodiesSeen.push(init.body);

      if (bodiesSeen.length === 1) {
        return { ok: false, status: 401, text: async () => 'expired' } as Response;
      }

      return { ok: true, status: 200, json: async () => ({ processedCount: 1, results: [] }) } as Response;
    }) as unknown as typeof fetch;

    const api = makeApi(true);
    await api.push([
      { clientOperationId: 'stable-op-id', operationType: 'recordMilking', occurredAt: '2026-08-03T06:00:00.000Z', payload: { liters: 10 } },
    ]);

    expect(bodiesSeen[0]).toBe(bodiesSeen[1]);
  });
});
