import React, { useState } from 'react';

import { BigButton, Body, Card, Notice, Screen, TextField, Title } from '../ui/components';
import type { AuthService } from '../services/authService';
import { useSingleFlight } from '../ui/useSingleFlight';

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
  const { busy, runOnce } = useSingleFlight();

  /*
   * Nothing here reaches the outbox, but the gesture is the same one (D2): two
   * taps on "Iniciar sesión" used to fire two login requests, and on the farm's
   * signal that means the second one lands on a phone that is already
   * authenticated — two answers, one of which arrives after the screen is gone.
   * The latch is the hook's, so the rule is the same everywhere in the app.
   */
  const run = (action: () => Promise<unknown>) =>
    runOnce(async () => {
      setError(null);
      try {
        await action();
        onAuthenticated();
      } catch (caught) {
        setError((caught as Error).message);
      }
    });

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
            onPress={() => void run(() => auth.unlockWithPin(pin))}
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
          onPress={() => void run(() => auth.login(email, password))}
        />
      </Card>
    </Screen>
  );
}
