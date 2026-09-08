import React, { useState } from 'react';

import { BigButton, Body, Card, Notice, Screen, TextField, Title } from '../ui/components';
import type { AuthService } from '../services/authService';

/**
 * Two ways in, because the farm has two situations.
 *
 * With signal, the employee logs in with their credentials and sets a PIN. Without signal
 * — which is most mornings — the PIN opens the cached session. A login screen that only
 * worked online would make the whole offline app unusable at exactly the hour it matters.
 */
export function LoginScreen({
  auth,
  hasCachedSession,
  onAuthenticated,
}: {
  auth: AuthService;
  hasCachedSession: boolean;
  onAuthenticated: () => void;
}) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      onAuthenticated();
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    /*
     * Scrollable: with the keyboard up on a short screen the "Iniciar sesión" button sits
     * under it, and there is nothing to drag. The scrollable mode also brings
     * keyboardShouldPersistTaps="handled", so the first tap on that button reaches it
     * instead of being spent dismissing the keyboard.
     */
    <Screen testID="login-screen" scrollable>
      <Title>HATO — Campo</Title>

      {error ? <Notice text={error} /> : null}

      {hasCachedSession ? (
        <Card>
          <Body>Entrar sin señal</Body>
          <TextField label="PIN" testID="pin-input" value={pin} onChangeText={setPin} secure />
          <BigButton
            testID="unlock-with-pin"
            label="Desbloquear"
            busy={busy}
            onPress={() => run(() => auth.unlockWithPin(pin))}
          />
        </Card>
      ) : null}

      <Card>
        <Body>Entrar con contraseña</Body>
        <TextField label="Correo" testID="email-input" value={email} onChangeText={setEmail} />
        <TextField
          label="Contraseña"
          testID="password-input"
          value={password}
          onChangeText={setPassword}
          secure
        />
        <BigButton
          testID="login"
          label="Iniciar sesión"
          tone={hasCachedSession ? 'neutral' : 'primary'}
          busy={busy}
          onPress={() => run(() => auth.login(email, password))}
        />
      </Card>
    </Screen>
  );
}
