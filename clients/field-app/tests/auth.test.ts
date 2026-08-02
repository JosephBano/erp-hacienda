import { AuthService } from '../src/services/authService';

describe('AuthService Offline & Session Tests', () => {
  let authService: AuthService;

  beforeEach(() => {
    authService = new AuthService();
    // Mock global fetch
    global.fetch = jest.fn();
  });

  test('loginOnline successful stores session and tokens', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        token: 'jwt-access-token-123',
        refreshToken: 'refresh-token-456',
        userId: 'user-guid-001',
        role: 'registrar',
      }),
    });

    const session = await authService.loginOnline(
      { email: 'empleado@finca.ec', password: 'password123' },
      'http://localhost:5000'
    );

    Assert.assertNotNull(session);
    expect(session.token).toBe('jwt-access-token-123');
    expect(session.refreshToken).toBe('refresh-token-456');
    expect(authService.isOffline()).toBe(false);
  });

  test('setupOfflinePin and unlockOfflineWithPin succeeds offline', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        token: 'jwt-access-token-123',
        refreshToken: 'refresh-token-456',
        userId: 'user-guid-001',
        role: 'registrar',
      }),
    });

    await authService.loginOnline(
      { email: 'empleado@finca.ec', password: 'password123' },
      'http://localhost:5000'
    );

    await authService.setupOfflinePin('1234');

    const offlineSession = await authService.unlockOfflineWithPin('1234');
    expect(offlineSession.email).toBe('empleado@finca.ec');
    expect(authService.isOffline()).toBe(true);
  });

  test('unlockOfflineWithPin fails on wrong PIN', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        token: 'jwt-access-token-123',
        refreshToken: 'refresh-token-456',
        userId: 'user-guid-001',
        role: 'registrar',
      }),
    });

    await authService.loginOnline(
      { email: 'empleado@finca.ec', password: 'password123' },
      'http://localhost:5000'
    );

    await authService.setupOfflinePin('1234');

    await expect(authService.unlockOfflineWithPin('9999')).rejects.toThrow('PIN incorrecto.');
  });
});

const Assert = {
  assertNotNull: (val: any) => expect(val).not.toBeNull(),
};
