export interface SyncOutboxItem {
  clientOperationId: string;
  operationType: string;
  occurredAt: string;
  payload: Record<string, any>;
  status: 'pending' | 'synced' | 'rejected';
  errorDetails?: string;
  createdAt: number;
}

export interface SyncEngineStats {
  pendingCount: number;
  syncedCount: number;
  rejectedCount: number;
}

export class SyncEngine {
  private static CURSOR_KEY = 'hato_sync_cursor';
  private static OUTBOX_KEY = 'hato_sync_outbox';

  private outboxItems: SyncOutboxItem[] = [];
  private storedCursor: string = '';

  constructor() {
    this.loadStateFromStorage();
  }

  async enqueueOperation(
    operationType: string,
    payload: Record<string, any>,
    occurredAt?: string
  ): Promise<SyncOutboxItem> {
    const item: SyncOutboxItem = {
      clientOperationId: this.generateUUID(),
      operationType,
      occurredAt: occurredAt || new Date().toISOString(),
      payload,
      status: 'pending',
      createdAt: Date.now(),
    };

    this.outboxItems.push(item);
    await this.persistOutbox();
    return item;
  }

  async performPush(baseUrl: string, token: string, deviceId = 'field-device-01'): Promise<{ processedCount: number; results: any[] }> {
    const pending = this.outboxItems.filter((i) => i.status === 'pending');
    if (pending.length === 0) {
      return { processedCount: 0, results: [] };
    }

    const payload = {
      deviceId,
      operations: pending.map((item) => ({
        clientOperationId: item.clientOperationId,
        operationType: item.operationType,
        occurredAt: item.occurredAt,
        payload: item.payload,
      })),
    };

    const response = await fetch(`${baseUrl}/api/v1/sync/push`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const err = await response.text();
      throw new Error(`Push sync failed: ${response.status} - ${err}`);
    }

    const data = await response.json();
    const serverResults: any[] = data.results || [];

    for (const res of serverResults) {
      const localItem = this.outboxItems.find((i) => i.clientOperationId === res.clientOperationId);
      if (localItem) {
        if (res.status === 'Accepted' || res.status === 'Duplicate') {
          localItem.status = 'synced';
        } else if (res.status === 'Rejected') {
          localItem.status = 'rejected';
          localItem.errorDetails = res.errorDetails || 'Error al procesar operación';
        }
      }
    }

    await this.persistOutbox();
    return { processedCount: data.processedCount || pending.length, results: serverResults };
  }

  async performPull(baseUrl: string, token: string): Promise<{ collections: any; cursor: string }> {
    const querySince = this.storedCursor ? `?since=${encodeURIComponent(this.storedCursor)}` : '';
    const response = await fetch(`${baseUrl}/api/v1/sync/pull${querySince}`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      const err = await response.text();
      throw new Error(`Pull sync failed: ${response.status} - ${err}`);
    }

    const data = await response.json();
    if (data.cursor) {
      this.storedCursor = data.cursor;
      this.persistCursor();
    }

    return { collections: data.collections, cursor: data.cursor };
  }

  async performSync(baseUrl: string, token: string): Promise<{ pushResults: any; pullResults: any }> {
    const pushResults = await this.performPush(baseUrl, token);
    const pullResults = await this.performPull(baseUrl, token);
    return { pushResults, pullResults };
  }

  getOutboxStats(): SyncEngineStats {
    return {
      pendingCount: this.outboxItems.filter((i) => i.status === 'pending').length,
      syncedCount: this.outboxItems.filter((i) => i.status === 'synced').length,
      rejectedCount: this.outboxItems.filter((i) => i.status === 'rejected').length,
    };
  }

  getRejectedItems(): SyncOutboxItem[] {
    return this.outboxItems.filter((i) => i.status === 'rejected');
  }

  getOutboxItems(): SyncOutboxItem[] {
    return [...this.outboxItems];
  }

  getStoredCursor(): string {
    return this.storedCursor;
  }

  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

  private async persistOutbox(): Promise<void> {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(SyncEngine.OUTBOX_KEY, JSON.stringify(this.outboxItems));
    }
  }

  private async persistCursor(): Promise<void> {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(SyncEngine.CURSOR_KEY, this.storedCursor);
    }
  }

  private loadStateFromStorage(): void {
    if (typeof localStorage !== 'undefined') {
      const rawOutbox = localStorage.getItem(SyncEngine.OUTBOX_KEY);
      if (rawOutbox) {
        try {
          this.outboxItems = JSON.parse(rawOutbox);
        } catch {
          this.outboxItems = [];
        }
      }

      const rawCursor = localStorage.getItem(SyncEngine.CURSOR_KEY);
      if (rawCursor) {
        this.storedCursor = rawCursor;
      }
    }
  }
}
