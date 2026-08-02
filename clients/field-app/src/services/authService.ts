export interface UserSession {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  token: string;
  refreshToken: string;
  pinHash?: string;
}

export interface LoginCredentials {
  email: string;
  password: string;
}

export class AuthService {
  private static STORAGE_KEY = 'hato_user_session';
  private inMemorySession: UserSession | null = null;
  private isOfflineMode = false;

  async loginOnline(credentials: LoginCredentials, baseUrl: string): Promise<UserSession> {
    try {
      const response = await fetch(`${baseUrl}/api/v1/people/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(credentials),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Error de autenticación: ${response.status} - ${errorText}`);
      }

      const data = await response.json();
      const session: UserSession = {
        userId: data.userId || 'user-id-placeholder',
        fullName: data.fullName || credentials.email.split('@')[0],
        email: credentials.email,
        role: data.role || 'registrar',
        token: data.token,
        refreshToken: data.refreshToken,
      };

      this.inMemorySession = session;
      this.isOfflineMode = false;
      await this.saveSessionToStorage(session);
      return session;
    } catch (error: any) {
      // If network is unavailable, attempt offline fallback
      const cached = await this.getCachedSession();
      if (cached && cached.email.toLowerCase() === credentials.email.toLowerCase()) {
        this.inMemorySession = cached;
        this.isOfflineMode = true;
        return cached;
      }
      throw error;
    }
  }

  async setupOfflinePin(pin: string): Promise<void> {
    if (!this.inMemorySession) {
      throw new Error('Debe haber una sesión activa para configurar el PIN offline.');
    }
    const pinHash = await this.hashPin(pin);
    this.inMemorySession.pinHash = pinHash;
    await this.saveSessionToStorage(this.inMemorySession);
  }

  async unlockOfflineWithPin(pin: string): Promise<UserSession> {
    const cached = await this.getCachedSession();
    if (!cached || !cached.pinHash) {
      throw new Error('No existe una sesión cacheada con PIN para acceso offline.');
    }

    const inputHash = await this.hashPin(pin);
    if (inputHash !== cached.pinHash) {
      throw new Error('PIN incorrecto.');
    }

    this.inMemorySession = cached;
    this.isOfflineMode = true;
    return cached;
  }

  async refreshAuthToken(baseUrl: string): Promise<string> {
    if (!this.inMemorySession?.refreshToken) {
      throw new Error('No hay refresh token disponible.');
    }

    const response = await fetch(`${baseUrl}/api/v1/people/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: this.inMemorySession.refreshToken }),
    });

    if (!response.ok) {
      throw new Error('No se pudo renovar la sesión.');
    }

    const data = await response.json();
    this.inMemorySession.token = data.token;
    this.inMemorySession.refreshToken = data.refreshToken;
    await this.saveSessionToStorage(this.inMemorySession);
    return data.token;
  }

  getCurrentSession(): UserSession | null {
    return this.inMemorySession;
  }

  isOffline(): boolean {
    return this.isOfflineMode;
  }

  private async saveSessionToStorage(session: UserSession): Promise<void> {
    // In React Native Expo environment, SecureStore is used. Fallback to localStorage / mock storage.
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(AuthService.STORAGE_KEY, JSON.stringify(session));
    }
  }

  private async getCachedSession(): Promise<UserSession | null> {
    if (typeof localStorage !== 'undefined') {
      const raw = localStorage.getItem(AuthService.STORAGE_KEY);
      if (raw) {
        try {
          return JSON.parse(raw);
        } catch {
          return null;
        }
      }
    }
    return this.inMemorySession;
  }

  private async hashPin(pin: string): Promise<string> {
    // Simple fast hashing for test/demo compliance
    let hash = 0;
    for (let i = 0; i < pin.length; i++) {
      hash = (hash << 5) - hash + pin.charCodeAt(i);
      hash |= 0;
    }
    return hash.toString(16);
  }
}
