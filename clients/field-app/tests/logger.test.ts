import { LoggerService } from '../src/services/loggerService';

describe('LoggerService LOPDP Local Log Tests', () => {
  let loggerService: LoggerService;

  beforeEach(() => {
    if (typeof localStorage !== 'undefined') {
      localStorage.clear();
    }
    loggerService = new LoggerService();
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

  test('clearLogs purges all local logs', () => {
    loggerService.logWarn('Advertencia de batería baja');
    loggerService.clearLogs();

    expect(loggerService.getLogs().length).toBe(0);
  });
});
