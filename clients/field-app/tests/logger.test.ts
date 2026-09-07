import { LoggerService } from '../src/services/loggerService';

describe('LoggerService LOPDP Local Log Tests', () => {
  let loggerService: LoggerService;

  beforeEach(() => {
    (global as any).__resetNativeMocks?.();
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    loggerService = new LoggerService();
    loggerService.clearLogs();
  });

  test('logError appends error entry without cloud telemetry', () => {
    loggerService.logError('Conexión fallida al sincronizar', { endpoint: '/api/v1/sync/push' });

    const logs = loggerService.getLogs();
    expect(logs.length).toBe(1);
    expect(logs[0].level).toBe('error');
    expect(logs[0].message).toBe('Conexión fallida al sincronizar');
  });

  test('exportLogsJson returns formatted JSON for local support', () => {
    loggerService.logInfo('App arrancada');
    const json = loggerService.exportLogsJson();

    expect(json).toContain('App arrancada');
    expect(json).toContain('info');
  });

  test('records UTC date, appVersion, schemaVersion, attemptId, failedStage, collection, counts and clientOperationId (T8.2)', () => {
    loggerService.logError('Fallo al aplicar lote', {
      attemptId: 'att-123',
      failedStage: 'apply',
      collection: 'animals',
      counts: { total: 15, applied: 10, failed: 5 },
      clientOperationId: 'op-456',
    });

    const logs = loggerService.getLogs();
    expect(logs.length).toBe(1);
    const [entry] = logs;
    expect(entry.timestamp).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/);
    expect(entry.appVersion).toBe('1.0.0');
    expect(entry.schemaVersion).toBe(11);
    expect(entry.attemptId).toBe('att-123');
    expect(entry.failedStage).toBe('apply');
    expect(entry.collection).toBe('animals');
    expect(entry.counts).toEqual({ total: 15, applied: 10, failed: 5 });
    expect(entry.clientOperationId).toBe('op-456');
  });

  test('enforces bounded retention, evicting oldest logs when capacity is exceeded (T8.3)', () => {
    for (let i = 0; i < 110; i++) {
      loggerService.logInfo(`Log número ${i}`);
    }

    const logs = loggerService.getLogs();
    expect(logs.length).toBe(100);
    // Oldest 10 evicted: first log is number 10
    expect(logs[0].message).toBe('Log número 10');
    expect(logs[99].message).toBe('Log número 109');
  });

  test('redacts JWT, passwords, secrets, and full payloads from log entries (T8.5)', () => {
    const rawJwt = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHpm_BvW9m_pQ';
    loggerService.logError('Error con datos sensibles', {
      password: 'super-secret-password',
      token: 'secret-token-123',
      apiKey: 'api-key-xyz',
      sessionData: rawJwt,
      payload: { sensitiveField: 'secret-data', weight: 450 },
      payloadJson: '{"key":"secret"}',
      normalField: 'ok-value',
    });

    const [entry] = loggerService.getLogs();
    const ctx = entry.context as any;
    expect(ctx.password).toBe('[REDACTED]');
    expect(ctx.token).toBe('[REDACTED]');
    expect(ctx.sessionData).toBe('[JWT REDACTED]');
    expect(ctx.payload).toBe('[PAYLOAD REDACTED]');
    expect(ctx.payloadJson).toBe('[PAYLOAD REDACTED]');
    expect(ctx.normalField).toBe('ok-value');

    // JSON export does not leak full payload or credentials
    const exported = loggerService.exportLogsJson();
    expect(exported).not.toContain('super-secret-password');
    expect(exported).not.toContain(rawJwt);
    expect(exported).not.toContain('secret-data');
  });

  test('without localStorage, a new LoggerService recovers the logs saved by the previous one (T8.6)', async () => {
    // Force localStorage absent
    const savedLocalStorage = (global as any).localStorage;
    delete (global as any).localStorage;

    try {
      const logger1 = new LoggerService();
      logger1.logError('Conexión fallida en campo', {
        failedStage: 'push',
        clientOperationId: 'op-recovered',
      });
      await logger1.flush();

      // Second instance without localStorage recovers from native SecureStore
      const logger2 = new LoggerService();
      await logger2.waitForReady();

      const recovered = logger2.getLogs();
      expect(recovered.length).toBe(1);
      expect(recovered[0].message).toBe('Conexión fallida en campo');
      expect(recovered[0].failedStage).toBe('push');
      expect(recovered[0].clientOperationId).toBe('op-recovered');
    } finally {
      if (savedLocalStorage) {
        (global as any).localStorage = savedLocalStorage;
      }
    }
  });
});
