import { SyncEngine } from '../src/services/syncEngine';

describe('SyncEngine Client Tests', () => {
  let syncEngine: SyncEngine;

  beforeEach(() => {
    // Clear localStorage mock if present
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    syncEngine = new SyncEngine();
    global.fetch = jest.fn();
  });

  test('enqueueOperation adds item to outbox with pending status', async () => {
    const item = await syncEngine.enqueueOperation('createAnimal', {
      sex: 'Female',
      speciesId: 'species-001',
    });

    expect(item.clientOperationId).toBeDefined();
    expect(item.operationType).toBe('createAnimal');
    expect(item.status).toBe('pending');

    const stats = syncEngine.getOutboxStats();
    expect(stats.pendingCount).toBe(1);
  });

  test('performPush sends pending outbox operations and marks accepted as synced', async () => {
    const item = await syncEngine.enqueueOperation('createAnimal', { sex: 'Female' });

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        processedCount: 1,
        results: [
          {
            clientOperationId: item.clientOperationId,
            status: 'Accepted',
            resultRef: 'animal-uuid-99',
          },
        ],
      }),
    });

    const pushRes = await syncEngine.performPush('http://localhost:5000', 'jwt-token-123');

    expect(pushRes.processedCount).toBe(1);
    const stats = syncEngine.getOutboxStats();
    expect(stats.pendingCount).toBe(0);
    expect(stats.syncedCount).toBe(1);
  });

  test('performPush marks rejected operations and preserves error details without deleting', async () => {
    const validItem = await syncEngine.enqueueOperation('createAnimal', { sex: 'Female' });
    const invalidItem = await syncEngine.enqueueOperation('createAnimal', { sex: 'Invalid' });

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        processedCount: 2,
        results: [
          {
            clientOperationId: validItem.clientOperationId,
            status: 'Accepted',
            resultRef: 'animal-uuid-01',
          },
          {
            clientOperationId: invalidItem.clientOperationId,
            status: 'Rejected',
            errorDetails: 'Especie no válida',
          },
        ],
      }),
    });

    await syncEngine.performPush('http://localhost:5000', 'jwt-token-123');

    const stats = syncEngine.getOutboxStats();
    expect(stats.syncedCount).toBe(1);
    expect(stats.rejectedCount).toBe(1);

    const rejected = syncEngine.getRejectedItems();
    expect(rejected.length).toBe(1);
    expect(rejected[0].clientOperationId).toBe(invalidItem.clientOperationId);
    expect(rejected[0].errorDetails).toBe('Especie no válida');
  });

  test('performPull updates stored cursor and returns collections', async () => {
    const mockNextCursor = '2026-08-02T16:00:00Z_uuid-123';

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        cursor: mockNextCursor,
        hasMore: false,
        collections: {
          animals: [{ id: 'a-1', sex: 'Female' }],
        },
      }),
    });

    const pullRes = await syncEngine.performPull('http://localhost:5000', 'jwt-token-123');

    expect(pullRes.cursor).toBe(mockNextCursor);
    expect(pullRes.collections.animals.length).toBe(1);
    expect(syncEngine.getStoredCursor()).toBe(mockNextCursor);
  });

  test('simulated dual clients push outbox and converge with pull', async () => {
    const client1 = new SyncEngine();
    const client2 = new SyncEngine();

    const op1 = await client1.enqueueOperation('recordMilking', { totalLiters: 20 });
    const op2 = await client2.enqueueOperation('recordMilking', { totalLiters: 15 });

    // Client 1 push
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        processedCount: 1,
        results: [{ clientOperationId: op1.clientOperationId, status: 'Accepted', resultRef: 'milk-1' }],
      }),
    });
    await client1.performPush('http://localhost:5000', 'token-1');

    // Client 2 push
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        processedCount: 1,
        results: [{ clientOperationId: op2.clientOperationId, status: 'Accepted', resultRef: 'milk-2' }],
      }),
    });
    await client2.performPush('http://localhost:5000', 'token-2');

    expect(client1.getOutboxStats().syncedCount).toBe(1);
    expect(client2.getOutboxStats().syncedCount).toBe(1);
  });
});
