import * as SecureStore from 'expo-secure-store';
import { SCHEMA_VERSION } from '../database/schema';

export const MAX_LOG_ENTRIES = 100;
export const APP_VERSION = '1.0.0';

export interface DiagnosticLogContext {
  attemptId?: string;
  failedStage?: string;
  collection?: string;
  counts?: Record<string, number>;
  clientOperationId?: string;
  operationType?: string;
  [key: string]: unknown;
}

export interface LocalLogEntry {
  timestamp: string;
  appVersion: string;
  schemaVersion: number;
  level: 'info' | 'warn' | 'error';
  message: string;
  attemptId?: string;
  failedStage?: string;
  collection?: string;
  counts?: Record<string, number>;
  clientOperationId?: string;
  context?: Record<string, unknown>;
}

const SENSITIVE_KEY_REGEX = /(password|token|secret|jwt|auth|bearer|credential)/i;
const JWT_REGEX = /^eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+$/;

export function sanitizeValue(key: string, value: unknown): unknown {
  if (SENSITIVE_KEY_REGEX.test(key)) {
    return '[REDACTED]';
  }

  // Redact full payloads or raw bodies to avoid leaking operator/business sensitive details
  if (key === 'payload' || key === 'fullPayload' || key === 'body' || key === 'payloadJson') {
    return '[PAYLOAD REDACTED]';
  }

  if (typeof value === 'string' && JWT_REGEX.test(value)) {
    return '[JWT REDACTED]';
  }

  if (Array.isArray(value)) {
    return value.map((item, idx) => sanitizeValue(String(idx), item));
  }

  if (value !== null && typeof value === 'object') {
    const sanitizedObj: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      sanitizedObj[k] = sanitizeValue(k, v);
    }
    return sanitizedObj;
  }

  return value;
}

export class LoggerService {
  private static LOGS_KEY = 'hato_local_error_logs';
  private logs: LocalLogEntry[] = [];
  private readonly appVersion: string;
  private readonly schemaVersion: number;
  private readyPromise: Promise<void>;
  private savePromise: Promise<void> = Promise.resolve();

  constructor(options: { appVersion?: string; schemaVersion?: number } = {}) {
    this.appVersion = options.appVersion ?? APP_VERSION;
    this.schemaVersion = options.schemaVersion ?? SCHEMA_VERSION;
    this.loadFromLocalStorageSync();
    this.readyPromise = this.loadLogs();
  }

  async waitForReady(): Promise<void> {
    await this.readyPromise;
  }

  logInfo(message: string, context?: DiagnosticLogContext | any): void {
    this.appendLog('info', message, context);
  }

  logWarn(message: string, context?: DiagnosticLogContext | any): void {
    this.appendLog('warn', message, context);
  }

  logError(message: string, context?: DiagnosticLogContext | any): void {
    this.appendLog('error', message, context);
  }

  info(message: string, context?: any): void {
    this.logInfo(message, context);
  }

  warn(message: string, context?: any): void {
    this.logWarn(message, context);
  }

  error(message: string, context?: any): void {
    this.logError(message, context);
  }

  getLogs(): LocalLogEntry[] {
    return [...this.logs];
  }

  async getLogsAsync(): Promise<LocalLogEntry[]> {
    await this.readyPromise;
    return this.getLogs();
  }

  exportLogsJson(): string {
    return JSON.stringify(this.logs, null, 2);
  }

  clearLogs(): void {
    this.logs = [];
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.removeItem(LoggerService.LOGS_KEY);
      } catch {}
    }
    this.savePromise = (async () => {
      try {
        if (typeof SecureStore !== 'undefined' && (await SecureStore.isAvailableAsync().catch(() => false))) {
          await SecureStore.deleteItemAsync(LoggerService.LOGS_KEY);
        }
      } catch {}
    })();
  }

  async flush(): Promise<void> {
    await this.savePromise;
  }

  private appendLog(level: 'info' | 'warn' | 'error', message: string, context?: any): void {
    let attemptId: string | undefined;
    let failedStage: string | undefined;
    let collection: string | undefined;
    let counts: Record<string, number> | undefined;
    let clientOperationId: string | undefined;
    let sanitizedContext: Record<string, unknown> | undefined;

    if (context && typeof context === 'object') {
      const c = context as Record<string, any>;
      attemptId = c.attemptId ? String(c.attemptId) : undefined;
      failedStage = c.failedStage ? String(c.failedStage) : undefined;
      collection = c.collection ? String(c.collection) : undefined;
      if (c.counts && typeof c.counts === 'object') {
        counts = c.counts;
      }
      clientOperationId = c.clientOperationId ? String(c.clientOperationId) : undefined;
      sanitizedContext = sanitizeValue('context', c) as Record<string, unknown>;
    }

    const entry: LocalLogEntry = {
      timestamp: new Date().toISOString(),
      appVersion: this.appVersion,
      schemaVersion: this.schemaVersion,
      level,
      message,
      attemptId,
      failedStage,
      collection,
      counts,
      clientOperationId,
      context: sanitizedContext,
    };

    this.logs.push(entry);

    // Bounded retention (T8.3)
    while (this.logs.length > MAX_LOG_ENTRIES) {
      this.logs.shift();
    }

    this.saveLogs();
  }

  private saveLogs(): void {
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.setItem(LoggerService.LOGS_KEY, JSON.stringify(this.logs));
      } catch {}
    }

    const payload = JSON.stringify(this.logs);
    this.savePromise = (async () => {
      try {
        if (typeof SecureStore !== 'undefined' && (await SecureStore.isAvailableAsync().catch(() => false))) {
          await SecureStore.setItemAsync(LoggerService.LOGS_KEY, payload);
        }
      } catch {}
    })();
  }

  private loadFromLocalStorageSync(): void {
    if (typeof localStorage !== 'undefined') {
      try {
        const raw = localStorage.getItem(LoggerService.LOGS_KEY);
        if (raw) {
          const parsed = JSON.parse(raw);
          if (Array.isArray(parsed)) {
            this.logs = parsed;
          }
        }
      } catch {}
    }
  }

  private async loadLogs(): Promise<void> {
    try {
      if (typeof SecureStore !== 'undefined' && (await SecureStore.isAvailableAsync().catch(() => false))) {
        const raw = await SecureStore.getItemAsync(LoggerService.LOGS_KEY);
        if (raw) {
          try {
            const parsed = JSON.parse(raw);
            if (Array.isArray(parsed)) {
              this.logs = parsed;
              return;
            }
          } catch {}
        }
      }
    } catch {}

    this.loadFromLocalStorageSync();
  }
}
