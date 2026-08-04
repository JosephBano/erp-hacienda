import * as Crypto from 'expo-crypto';
import * as SecureStore from 'expo-secure-store';

import { newUuid } from './identifiers';

const SESSION_KEY = 'hato_session';
const MIN_PIN_LENGTH = 4;

export interface UserSession {
  userId: string;
  fullName: string;
  email: string;
  token: string;
  refreshToken: string;
  expiresAt: string;
  roles: string[];
  permissions: string[];
  pinHash?: string;
  pinSalt?: string;
}

/**
 * Session for a phone that spends its day out of range.
 *
 * The session lives in the device keystore (`expo-secure-store`), which is the only
 * storage React Native actually has — an earlier version wrote to `localStorage`, a
 * browser API that simply does not exist here, so nothing was ever persisted and every
 * restart forced a login the employee could not perform without signal.
 *
 * The unlock PIN is stored as a salted SHA-256 digest. A 4-digit PIN is a small secret by
 * nature; the salt at least means a stolen phone's hash cannot be matched against another
 * employee's, and the digest means the PIN itself is never written down.
 */
export class AuthService {
  private session: UserSession | null = null;
  private offline = false;

  constructor(private readonly baseUrl: string) {}

  currentSession(): UserSession | null {
    return this.session;
  }

  isOffline(): boolean {
    return this.offline;
  }

  token(): string | null {
    return this.session?.token ?? null;
  }

  /** Loads whatever the keystore holds. Called on app start, before showing any screen. */
  async restore(): Promise<UserSession | null> {
    const raw = await SecureStore.getItemAsync(SESSION_KEY);
    if (!raw) return null;

    try {
      this.session = JSON.parse(raw) as UserSession;
    } catch {
      // A corrupt entry must not brick the app; the employee logs in again.
      await SecureStore.deleteItemAsync(SESSION_KEY);
      return null;
    }

    return this.session;
  }

  async login(email: string, password: string): Promise<UserSession> {
    const response = await fetch(`${this.baseUrl}/api/v1/people/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      throw new Error(`No se pudo iniciar sesión (${response.status}).`);
    }

    const data = await response.json();
    const existing = this.session;

    this.session = {
      userId: data.userId,
      fullName: data.fullName,
      email,
      token: data.token,
      refreshToken: data.refreshToken,
      expiresAt: data.expiresAt,
      roles: data.roles ?? [],
      permissions: data.permissions ?? [],
      // A re-login by the same employee keeps their PIN; a different one starts clean.
      pinHash: existing?.email === email ? existing.pinHash : undefined,
      pinSalt: existing?.email === email ? existing.pinSalt : undefined,
    };

    this.offline = false;
    await this.persist();
    return this.session;
  }

  async setPin(pin: string): Promise<void> {
    if (!this.session) {
      throw new Error('Debe haber una sesión activa para configurar el PIN.');
    }

    if (!/^\d+$/.test(pin) || pin.length < MIN_PIN_LENGTH) {
      throw new Error(`El PIN debe tener al menos ${MIN_PIN_LENGTH} dígitos numéricos.`);
    }

    const salt = newUuid();
    this.session.pinSalt = salt;
    this.session.pinHash = await digest(pin, salt);
    await this.persist();
  }

  /** Opens the cached session with no network at all. */
  async unlockWithPin(pin: string): Promise<UserSession> {
    const session = this.session ?? (await this.restore());

    if (!session?.pinHash || !session.pinSalt) {
      throw new Error('No hay una sesión guardada con PIN en este dispositivo.');
    }

    const candidate = await digest(pin, session.pinSalt);
    if (candidate !== session.pinHash) {
      throw new Error('PIN incorrecto.');
    }

    this.session = session;
    this.offline = true;
    return session;
  }

  /**
   * Returns whether a usable token is now in hand. It reports rather than throws because
   * the caller is the sync engine mid-batch, and a failed renewal is a reason to stop
   * syncing, not an error to propagate through the queue.
   */
  async refresh(): Promise<boolean> {
    if (!this.session?.refreshToken) return false;

    try {
      const response = await fetch(`${this.baseUrl}/api/v1/people/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: this.session.refreshToken }),
      });

      if (!response.ok) return false;

      const data = await response.json();
      this.session = {
        ...this.session,
        token: data.token,
        refreshToken: data.refreshToken,
        expiresAt: data.expiresAt ?? this.session.expiresAt,
      };

      this.offline = false;
      await this.persist();
      return true;
    } catch {
      return false;
    }
  }

  async logout(): Promise<void> {
    this.session = null;
    this.offline = false;
    await SecureStore.deleteItemAsync(SESSION_KEY);
  }

  /** Test seam: lets a test assert what actually reached the keystore. */
  async debugStoredSessionJson(): Promise<string | null> {
    return SecureStore.getItemAsync(SESSION_KEY);
  }

  private async persist(): Promise<void> {
    if (!this.session) return;
    await SecureStore.setItemAsync(SESSION_KEY, JSON.stringify(this.session));
  }
}

async function digest(pin: string, salt: string): Promise<string> {
  return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, `${salt}:${pin}`);
}
