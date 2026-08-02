export interface LocalLogEntry {
  timestamp: string;
  level: 'info' | 'warn' | 'error';
  message: string;
  context?: any;
}

export class LoggerService {
  private static LOGS_KEY = 'hato_local_error_logs';
  private logs: LocalLogEntry[] = [];

  constructor() {
    this.loadLogs();
  }

  logInfo(message: string, context?: any): void {
    this.appendLog('info', message, context);
  }

  logWarn(message: string, context?: any): void {
    this.appendLog('warn', message, context);
  }

  logError(message: string, context?: any): void {
    this.appendLog('error', message, context);
  }

  getLogs(): LocalLogEntry[] {
    return [...this.logs];
  }

  exportLogsJson(): string {
    return JSON.stringify(this.logs, null, 2);
  }

  clearLogs(): void {
    this.logs = [];
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(LoggerService.LOGS_KEY);
    }
  }

  private appendLog(level: 'info' | 'warn' | 'error', message: string, context?: any): void {
    const entry: LocalLogEntry = {
      timestamp: new Date().toISOString(),
      level,
      message,
      context,
    };

    this.logs.push(entry);
    // Keep max 200 logs locally
    if (this.logs.length > 200) {
      this.logs.shift();
    }
    this.saveLogs();
  }

  private saveLogs(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(LoggerService.LOGS_KEY, JSON.stringify(this.logs));
    }
  }

  private loadLogs(): void {
    if (typeof localStorage !== 'undefined') {
      const raw = localStorage.getItem(LoggerService.LOGS_KEY);
      if (raw) {
        try {
          this.logs = JSON.parse(raw);
        } catch {
          this.logs = [];
        }
      }
    }
  }
}
