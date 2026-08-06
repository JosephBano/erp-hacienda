export interface PushOperation {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  payload: Record<string, unknown>;
}

export type PushStatus = 'Accepted' | 'Duplicate' | 'Rejected';

export interface PushResult {
  clientOperationId: string;
  status: PushStatus;
  resultRef: string | null;
  errorDetails: string | null;
}

export interface PushResponse {
  processedCount: number;
  results: PushResult[];
}

export interface PullResponse {
  cursor: string;
  hasMore: boolean;
  collections: Record<string, Array<Record<string, unknown>>>;
}

/**
 * The server as the sync engine needs it. Keeping this an interface is what lets the
 * engine's tests drive real failure modes — a push that never answers, a server that
 * refuses one record out of ten — instead of asserting against a mocked `fetch`.
 */
export interface SyncApi {
  push(operations: PushOperation[]): Promise<PushResponse>;
  pull(since?: string, batchSize?: number): Promise<PullResponse>;
}

export class AuthenticationExpiredError extends Error {
  constructor() {
    super('La sesión expiró durante la sincronización.');
    this.name = 'AuthenticationExpiredError';
  }
}

export interface HttpSyncApiOptions {
  baseUrl: string;
  /** Current bearer token. Read on every call so a refresh mid-sync is picked up. */
  getToken: () => string | null;
  /** Returns true if it managed to obtain a fresh token. */
  refreshToken: () => Promise<boolean>;
  deviceId: string;
}

/** Talks to `/api/v1/sync` (ADR-0008). */
export class HttpSyncApi implements SyncApi {
  constructor(private readonly options: HttpSyncApiOptions) {}

  async push(operations: PushOperation[]): Promise<PushResponse> {
    return this.request<PushResponse>('/api/v1/sync/push', {
      method: 'POST',
      body: JSON.stringify({ deviceId: this.options.deviceId, operations }),
    });
  }

  async pull(since?: string, batchSize?: number): Promise<PullResponse> {
    const query = new URLSearchParams();
    if (since) query.set('since', since);
    if (batchSize) query.set('batchSize', String(batchSize));

    const suffix = query.toString() ? `?${query.toString()}` : '';
    return this.request<PullResponse>(`/api/v1/sync/pull${suffix}`, { method: 'GET' });
  }

  /**
   * Retries exactly once after refreshing an expired token. The retry is safe because
   * every operation carries its `clientOperationId`: the server recognises the replay and
   * answers "duplicate" rather than writing the record twice.
   */
  private async request<T>(path: string, init: RequestInit, isRetry = false): Promise<T> {
    const response = await fetch(`${this.options.baseUrl}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.options.getToken() ?? ''}`,
      },
    });

    if (response.status === 401) {
      if (isRetry) {
        // The freshest token available was still refused: whatever the server's reason,
        // from the employee's side this is indistinguishable from "session expired" and
        // must surface as the same error, not a generic one the UI wouldn't recognise.
        throw new AuthenticationExpiredError();
      }

      const refreshed = await this.options.refreshToken();
      if (refreshed) {
        return this.request<T>(path, init, true);
      }

      throw new AuthenticationExpiredError();
    }

    if (!response.ok) {
      const detail = await response.text();
      throw new Error(`Sincronización falló (${response.status}): ${detail}`);
    }

    return (await response.json()) as T;
  }
}
