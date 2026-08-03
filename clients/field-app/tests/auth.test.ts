import { AuthService } from '../src/services/authService';

/**
 * The employee unlocks the app at 5 AM with a PIN and no signal. Two things must hold:
 * the session has to survive being closed (it lives in the device keystore, not in a
 * browser API React Native does not have), and the PIN must be stored as a salted
 * cryptographic digest — the previous implementation used a 32-bit string hash, which a
 * phone can exhaust for every 4-digit PIN essentially instantly.
 */
describe('AuthService', () => {
  const baseUrl = 'https://hato.test';
  let service: AuthService;

  const loginResponse = {
    token: 'jwt-token',
    refreshToken: 'refresh-token',
    expiresAt: '2030-01-01T00:00:00Z',
    userId: 'user-1',
    fullName: 'María Campo',
    roles: ['registrador'],
    permissions: ['livestock.animals.write'],
  };

  beforeEach(() => {
    (global as any).__resetNativeMocks();
    service = new AuthService(baseUrl);
    global.fetch = jest.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => loginResponse,
      text: async () => '',
    })) as unknown as typeof fetch;
  });

  it('keeps the session after a successful login', async () => {
    const session = await service.login('maria@finca.ec', 'Secreta123!');

    expect(session.fullName).toBe('María Campo');
    expect(service.currentSession()?.token).toBe('jwt-token');
  });

  it('restores the session from the device keystore when the app reopens', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');

    const reopened = new AuthService(baseUrl);
    await reopened.restore();

    expect(reopened.currentSession()?.userId).toBe('user-1');
  });

  it('reports being offline when the login request cannot reach the server', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');
    await service.setPin('4321');

    global.fetch = jest.fn(async () => {
      throw new Error('Network request failed');
    }) as unknown as typeof fetch;

    const reopened = new AuthService(baseUrl);
    await reopened.restore();
    const session = await reopened.unlockWithPin('4321');

    expect(session.userId).toBe('user-1');
    expect(reopened.isOffline()).toBe(true);
  });

  it('refuses the wrong PIN', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');
    await service.setPin('4321');

    const reopened = new AuthService(baseUrl);
    await reopened.restore();

    await expect(reopened.unlockWithPin('0000')).rejects.toThrow(/PIN/i);
  });

  it('never stores the PIN itself', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');
    await service.setPin('4321');

    const stored = await service.debugStoredSessionJson();

    expect(stored).not.toContain('4321');
  });

  it('salts the digest so two employees with the same PIN do not share a hash', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');
    await service.setPin('4321');
    const first = JSON.parse((await service.debugStoredSessionJson())!).pinHash;

    (global as any).__resetNativeMocks();
    const other = new AuthService(baseUrl);
    await other.login('jose@finca.ec', 'Secreta123!');
    await other.setPin('4321');
    const second = JSON.parse((await other.debugStoredSessionJson())!).pinHash;

    expect(first).not.toBe(second);
  });

  it('rejects a PIN that is too short to be worth anything', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');

    await expect(service.setPin('12')).rejects.toThrow(/PIN/i);
  });

  it('refuses to unlock when no session was ever cached', async () => {
    await expect(service.unlockWithPin('4321')).rejects.toThrow(/sesión/i);
  });

  it('renews the token and keeps the new refresh token', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');

    global.fetch = jest.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => ({ ...loginResponse, token: 'jwt-2', refreshToken: 'refresh-2' }),
      text: async () => '',
    })) as unknown as typeof fetch;

    const refreshed = await service.refresh();

    expect(refreshed).toBe(true);
    expect(service.currentSession()?.token).toBe('jwt-2');
  });

  it('reports a failed renewal instead of throwing at the sync engine', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');

    global.fetch = jest.fn(async () => ({
      ok: false,
      status: 401,
      json: async () => ({}),
      text: async () => 'expired',
    })) as unknown as typeof fetch;

    expect(await service.refresh()).toBe(false);
  });

  it('forgets everything on logout', async () => {
    await service.login('maria@finca.ec', 'Secreta123!');

    await service.logout();

    expect(service.currentSession()).toBeNull();
    expect(await service.debugStoredSessionJson()).toBeNull();
  });
});
