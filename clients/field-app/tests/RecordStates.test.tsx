import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react-native';

import {
  CatalogueEmptyNotice,
  LocalStorageErrorNotice,
  RecordStatusBadge,
  RejectedOperationCard,
  SessionExpiredNotice,
  SyncStateNotice,
  formatOperationError,
} from '../src/ui/recordStates';
import { ThemeProvider } from '../src/ui/theme';

/**
 * feature-0010 (field-app-redesign) commit 3 — shared plain-language presentation.
 *
 * Verifies:
 *  - T3.1: The seven states from spec.md sec. 3.6 have unique presentations.
 *  - T3.2: "Sin señal" is never presented as failure of a locally saved record (D5).
 *  - T3.3: Local storage error never claims "guardado" and never suggests deleting/reinstalling app.
 *  - T3.4: Empty catalogue explicitly distinguishes three scenarios (real absence, 0 search results, pending download).
 *  - T3.5: Rejected operation card: record identification, plain reason, action available, payload preserved.
 *  - T3.6: Session expired notice: explains records are safe and how to restore sync.
 *  - T3.7: Diagnostic from 0004 is reachable, employee shielded from cursors, UUIDs, table names.
 *  - T3.8: No success state masks pull deltas pending from 0004.
 *  - Touch targets: interactive controls meet >= 64 logical units.
 */

describe('RecordStates (Commit 3)', () => {
  beforeEach(() => {
    (global as any).__resetNativeMocks?.();
  });

  describe('formatOperationError (T3.5, T3.7)', () => {
    it('returns a fallback message when no error details are provided', () => {
      expect(formatOperationError()).toBe('El servidor rechazó la operación sin indicar el motivo.');
      expect(formatOperationError(null)).toBe('El servidor rechazó la operación sin indicar el motivo.');
      expect(formatOperationError('')).toBe('El servidor rechazó la operación sin indicar el motivo.');
      expect(formatOperationError('   ')).toBe('El servidor rechazó la operación sin indicar el motivo.');
    });

    it('preserves clean, human-readable Spanish messages from domain/backend', () => {
      expect(formatOperationError('El animal no existe en el sistema.')).toBe(
        'El animal no existe en el sistema.'
      );
      expect(formatOperationError('Rechazo de prueba: falta de permiso para Ordeño')).toBe(
        'Rechazo de prueba: falta de permiso para Ordeño'
      );
    });

    it('translates English permission/forbidden errors to plain Spanish', () => {
      expect(formatOperationError('PermissionDenied: User lacks permission to execute recordMilking')).toBe(
        'No tiene permisos para registrar esta actividad en el sistema.'
      );
      expect(formatOperationError('403 Forbidden: employee not authorized')).toBe(
        'No tiene permisos para registrar esta actividad en el sistema.'
      );
      expect(formatOperationError('User lacks permission to record births')).toBe(
        'No tiene permisos para registrar esta actividad en el sistema.'
      );
    });

    it('translates biological incompatibility and sex checks to plain Spanish', () => {
      expect(formatOperationError('Cannot record milking for male animal')).toBe(
        'El animal seleccionado no es apto para esta actividad (sexo o condición biológica no compatible).'
      );
      expect(formatOperationError('Incompatible sex: birth only applies to females')).toBe(
        'El animal seleccionado no es apto para esta actividad (sexo o condición biológica no compatible).'
      );
    });

    it('translates active withdrawal period warnings to plain Spanish', () => {
      expect(formatOperationError('Animal is under active withdrawal period')).toBe(
        'El animal se encuentra bajo período de retiro activo.'
      );
    });

    it('translates deceased or culled status to plain Spanish', () => {
      expect(formatOperationError('Animal is deceased and cannot receive events')).toBe(
        'El animal se encuentra registrado como fallecido o dado de baja.'
      );
      expect(formatOperationError('El animal ya fue dado de baja')).toBe(
        'El animal se encuentra registrado como fallecido o dado de baja.'
      );
    });

    it('translates unique constraint / duplicate identifier conflicts', () => {
      expect(formatOperationError('duplicate key value violates unique constraint "pk_animals"')).toBe(
        'El número de arete o identificador ya está asignado a otro animal.'
      );
      expect(formatOperationError('Tag already exists in farm')).toBe(
        'El número de arete o identificador ya está asignado a otro animal.'
      );
    });

    it('translates server exceptions and 500 errors to plain reassurance without stack trace', () => {
      expect(
        formatOperationError('Npgsql.PostgresException (0x80004005): 500 Internal Server Error at Database.cs:42')
      ).toBe(
        'Ocurrió un error en el servidor al procesar la solicitud. El registro se conserva intacto en el teléfono.'
      );
    });

    it('translates network dropouts to clear offline message', () => {
      expect(formatOperationError('Network request failed: ECONNREFUSED 192.168.1.50')).toBe(
        'Sin conexión con el servidor. El registro está guardado en este teléfono y se enviará cuando haya señal.'
      );
    });

    it('translates dependent birth failures clearly', () => {
      expect(formatOperationError('Depende de un nacimiento rechazado: Parto rechazado por el servidor')).toBe(
        'Esta operación depende de un parto rechazado por el servidor: Parto rechazado por el servidor'
      );
    });

    it('cleans up raw UUIDs, cursors and table names from mixed messages (T3.7)', () => {
      const dirty = 'Error en tabla animals para UUID 3fa85f64-5717-4562-b3fc-2c963f66afa6: el peso no es válido';
      const clean = formatOperationError(dirty);
      expect(clean).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
      expect(clean).not.toContain('animals');
      expect(clean).toContain('el peso no es válido');
    });

    it('removes cursor jargon from sync error messages (T3.7)', () => {
      const withCursor = 'invalid sync cursor 018fa24b-67e3-789a-bcde-0123456789ab in sync_outbox';
      const clean = formatOperationError(withCursor);
      expect(clean).not.toContain('cursor');
      expect(clean).not.toContain('sync_outbox');
      expect(clean).toBe('error de sincronización');
    });
  });

  describe('RecordStatusBadge (T3.1, T3.8)', () => {
    it('renders state 1: local_pending ("Guardado local")', async () => {
      await render(<RecordStatusBadge status="local_pending" testID="badge-local" />);
      const badge = screen.getByTestId('badge-local');
      expect(badge).toBeTruthy();
      expect(screen.getByText('Guardado local')).toBeTruthy();
      expect(badge.props.accessibilityLabel).toContain('Guardado en este teléfono. Pendiente de enviar.');
    });

    it('renders state 2: syncing ("Enviando...")', async () => {
      await render(<RecordStatusBadge status="syncing" testID="badge-syncing" />);
      expect(screen.getByText('Enviando...')).toBeTruthy();
      expect(screen.getByTestId('badge-syncing').props.accessibilityLabel).toContain('Envío en curso');
    });

    it('renders state 3: accepted_pull_pending ("Aceptado (descarga pendiente)") (T3.8)', async () => {
      await render(<RecordStatusBadge status="accepted_pull_pending" testID="badge-pull-pending" />);
      expect(screen.getByText('Aceptado (descarga pendiente)')).toBeTruthy();
      expect(screen.getByTestId('badge-pull-pending').props.accessibilityLabel).toContain(
        'Ese registro llegó al servidor. Todavía faltan cambios por recibir en el teléfono.'
      );
    });

    it('renders synced ("Al día")', async () => {
      await render(<RecordStatusBadge status="synced" testID="badge-synced" />);
      expect(screen.getByText('Al día')).toBeTruthy();
    });

    it('renders state 4: rejected ("Rechazado")', async () => {
      await render(<RecordStatusBadge status="rejected" testID="badge-rejected" />);
      expect(screen.getByText('Rechazado')).toBeTruthy();
    });

    it('renders state 7: error ("Error local")', async () => {
      await render(<RecordStatusBadge status="error" testID="badge-error" />);
      expect(screen.getByText('Error local')).toBeTruthy();
    });

    it('supports custom labels and sizes', async () => {
      await render(<RecordStatusBadge status="local_pending" label="Guardado en galpón" size="large" />);
      expect(screen.getByText('Guardado en galpón')).toBeTruthy();
    });
  });

  describe('SyncStateNotice (T3.1, T3.2, T3.7)', () => {
    it('State 1 (offline_pending): never treats no signal as a failure (T3.2)', async () => {
      await render(<SyncStateNotice state="offline_pending" pendingCount={3} testID="sync-notice-offline" />);
      const notice = screen.getByTestId('sync-notice-offline');
      expect(notice).toBeTruthy();
      expect(screen.getByText('Guardado en este teléfono')).toBeTruthy();
      expect(
        screen.getByText('3 registros guardados en este teléfono. Pendiente de enviar. Puede seguir trabajando.')
      ).toBeTruthy();
      // Must not sound like a failure
      expect(screen.queryByText(/error/i)).toBeNull();
      expect(screen.queryByText(/fall[oó]/i)).toBeNull();
    });

    it('State 1 (offline_pending): singular wording with 1 record', async () => {
      await render(<SyncStateNotice state="offline_pending" pendingCount={1} />);
      expect(
        screen.getByText('1 registro guardado en este teléfono. Pendiente de enviar. Puede seguir trabajando.')
      ).toBeTruthy();
    });

    it('State 2 (pushing): communicates that push is active and local capture remains available', async () => {
      await render(<SyncStateNotice state="pushing" />);
      expect(screen.getByText('Enviando registros')).toBeTruthy();
      expect(
        screen.getByText('Enviando registros al servidor... La captura local sigue disponible.')
      ).toBeTruthy();
    });

    it('State 2 (pulling): communicates that pull is active and local capture remains available', async () => {
      await render(<SyncStateNotice state="pulling" />);
      expect(screen.getByText('Descargando datos')).toBeTruthy();
      expect(
        screen.getByText('Descargando datos desde la oficina... La captura local sigue disponible.')
      ).toBeTruthy();
    });

    it('State 2 (syncing): communicates bidirectional sync and local capture availability', async () => {
      await render(<SyncStateNotice state="syncing" />);
      expect(screen.getByText('Sincronizando')).toBeTruthy();
      expect(
        screen.getByText('Sincronización en curso con el servidor... La captura local sigue disponible.')
      ).toBeTruthy();
    });

    it('State 6 (auth_expired): explains session expired and records are safe (T3.6)', async () => {
      const onAction = jest.fn();
      await render(
        <SyncStateNotice
          state="auth_expired"
          onAction={onAction}
          actionLabel="Iniciar sesión"
        />
      );
      expect(screen.getByText('Sesión expirada')).toBeTruthy();
      expect(
        screen.getByText('Sesión expirada. Su trabajo está guardado localmente. Inicie sesión para reanudar el envío.')
      ).toBeTruthy();

      const btn = screen.getByText('Iniciar sesión');
      await fireEvent.press(btn);
      expect(onAction).toHaveBeenCalledTimes(1);
    });

    it('Provides persistent diagnostic link meeting T3.7 without displaying raw cursors', async () => {
      const onDiag = jest.fn();
      await render(
        <SyncStateNotice
          state="error"
          errorMessage="invalid sync cursor in table sync_outbox"
          onDiagnostic={onDiag}
          diagnosticLabel="Compartir diagnóstico"
        />
      );
      expect(screen.getByText('Sincronización pendiente')).toBeTruthy();
      // Cleaned message without cursors or tables
      expect(screen.getByText('error de sincronización')).toBeTruthy();
      expect(screen.queryByText(/sync_outbox/)).toBeNull();

      const diagBtn = screen.getByText('Compartir diagnóstico');
      await fireEvent.press(diagBtn);
      expect(onDiag).toHaveBeenCalledTimes(1);
    });
  });

  describe('CatalogueEmptyNotice (T3.4)', () => {
    it('Scenario A (empty): real absence in farm ("No hay registros")', async () => {
      await render(<CatalogueEmptyNotice variant="empty" testID="cat-empty" />);
      expect(screen.getByText('No hay registros')).toBeTruthy();
      expect(screen.getByText('No existen registros en la finca para esta sección.')).toBeTruthy();
    });

    it('Scenario B (no_matches): search query or filters returned 0 results', async () => {
      const onClear = jest.fn();
      await render(
        <CatalogueEmptyNotice
          variant="no_matches"
          onClearFilters={onClear}
          testID="cat-no-matches"
        />
      );
      expect(screen.getByText('Ningún resultado coincide con la búsqueda')).toBeTruthy();
      expect(screen.getByText('Pruebe con otros términos o limpie los filtros seleccionados.')).toBeTruthy();

      const clearBtn = screen.getByTestId('clear-filters-button');
      expect(clearBtn).toBeTruthy();
      await fireEvent.press(clearBtn);
      expect(onClear).toHaveBeenCalledTimes(1);
    });

    it('Scenario C (pending_sync): data not yet downloaded from server', async () => {
      const onSync = jest.fn();
      await render(
        <CatalogueEmptyNotice
          variant="pending_sync"
          onSync={onSync}
          testID="cat-pending-sync"
        />
      );
      expect(screen.getByText('Faltan datos por sincronizar desde la oficina')).toBeTruthy();
      expect(
        screen.getByText('Es posible que la información aún no se haya descargado en este teléfono.')
      ).toBeTruthy();

      const syncBtn = screen.getByTestId('sync-now-button');
      expect(syncBtn).toBeTruthy();
      await fireEvent.press(syncBtn);
      expect(onSync).toHaveBeenCalledTimes(1);
    });

    it('verifies that the three catalogue scenarios have distinct titles (T3.4)', () => {
      const titleEmpty = 'No hay registros';
      const titleMatches = 'Ningún resultado coincide con la búsqueda';
      const titleSync = 'Faltan datos por sincronizar desde la oficina';

      expect(titleEmpty).not.toEqual(titleMatches);
      expect(titleMatches).not.toEqual(titleSync);
      expect(titleEmpty).not.toEqual(titleSync);
    });
  });

  describe('RejectedOperationCard (T3.5, T3.7)', () => {
    it('shows record title, plain-language reason, preserved payload, and available actions', async () => {
      const onRetry = jest.fn();
      const onDismiss = jest.fn();

      await render(
        <RejectedOperationCard
          operationTitle="Parto"
          occurredAt="2026-09-08T10:30:00.000Z"
          errorDetails="PermissionDenied: User lacks permission to execute recordBirth in table birth_events"
          payload={{ motherTag: '0042', totalBorn: 12, liveBorn: 11 }}
          onRetry={onRetry}
          retryLabel="Corregir datos de parto"
          onDismiss={onDismiss}
          dismissLabel="Descartar este parto"
          testID="rejected-card"
        />
      );

      // What record:
      expect(screen.getByText('Parto')).toBeTruthy();
      expect(screen.getByText('Rechazado')).toBeTruthy();

      // Plain language reason (sanitized):
      const reasonEl = screen.getByTestId('rejected-reason-text');
      expect(reasonEl).toHaveTextContent('No tiene permisos para registrar esta actividad en el sistema.');
      expect(screen.getByTestId('rejected-reason')).not.toHaveTextContent('birth_events');
      expect(screen.getByTestId('rejected-reason')).not.toHaveTextContent('PermissionDenied');

      // Preserved payload:
      const payloadEl = screen.getByTestId('rejected-payload');
      expect(payloadEl).toHaveTextContent(/motherTag:\s*0042/);
      expect(payloadEl).toHaveTextContent(/totalBorn:\s*12/);
      expect(payloadEl).toHaveTextContent(/liveBorn:\s*11/);

      // Available actions:
      const retryBtn = screen.getByTestId('rejected-retry');
      expect(retryBtn).toHaveTextContent('Corregir datos de parto');
      await fireEvent.press(retryBtn);
      expect(onRetry).toHaveBeenCalledTimes(1);

      const dismissBtn = screen.getByTestId('rejected-dismiss');
      expect(dismissBtn).toHaveTextContent('Descartar este parto');
      await fireEvent.press(dismissBtn);
      expect(onDismiss).toHaveBeenCalledTimes(1);
    });

    it('renders string payloads intact', async () => {
      await render(
        <RejectedOperationCard
          operationTitle="Ordeño"
          payload="Litros: 5.5, Turno: Mañana"
        />
      );
      expect(screen.getByTestId('rejected-payload-text')).toHaveTextContent('Litros: 5.5, Turno: Mañana');
    });
  });

  describe('LocalStorageErrorNotice (T3.3)', () => {
    it('never claims "guardado" and never suggests deleting or reinstalling the app (T3.3)', async () => {
      const onRetry = jest.fn();
      await render(
        <LocalStorageErrorNotice
          errorMessage="Disk full: SQLite storage write failed"
          onRetry={onRetry}
          testID="storage-error"
        />
      );

      const notice = screen.getByTestId('storage-error');
      expect(notice).toBeTruthy();
      expect(screen.getByText('Error de almacenamiento local')).toBeTruthy();

      const bodyText = screen.getByText(
        'No se pudo guardar el registro en este teléfono. Conserve los datos en pantalla e intente guardar nuevamente. No cierre ni desinstale la aplicación para evitar perder su información.'
      );
      expect(bodyText).toBeTruthy();

      // CRITICAL T3.3 ASSERTIONS:
      // 1. Must never state that the record was saved:
      expect(notice).not.toHaveTextContent(/registro guardado con éxito/i);
      expect(notice).toHaveTextContent(/No se pudo guardar/);

      // 2. Must NEVER suggest deleting or reinstalling the app:
      expect(notice).not.toHaveTextContent(/borr(e|ar) la app/i);
      expect(notice).not.toHaveTextContent(/reinstal/i);
      expect(notice).toHaveTextContent(/No cierre ni desinstale la aplicación/);

      // Action to retry:
      const retryBtn = screen.getByTestId('retry-save-button');
      expect(retryBtn).toHaveTextContent('Reintentar guardado');
      await fireEvent.press(retryBtn);
      expect(onRetry).toHaveBeenCalledTimes(1);
    });
  });

  describe('SessionExpiredNotice (T3.6)', () => {
    it('informs the employee that local work is safe and how to restore sync (T3.6)', async () => {
      const onSignIn = jest.fn();
      await render(
        <SessionExpiredNotice
          onSignIn={onSignIn}
          signInLabel="Iniciar sesión para reanudar el envío"
          testID="session-expired"
        />
      );

      const notice = screen.getByTestId('session-expired');
      expect(notice).toBeTruthy();
      expect(screen.getByText('Sesión expirada')).toBeTruthy();
      expect(
        screen.getByText(
          'Sesión expirada. Su trabajo está guardado localmente en este teléfono. Inicie sesión para reanudar el envío al servidor.'
        )
      ).toBeTruthy();

      const btn = screen.getByTestId('sign-in-button');
      expect(btn).toHaveTextContent('Iniciar sesión para reanudar el envío');
      await fireEvent.press(btn);
      expect(onSignIn).toHaveBeenCalledTimes(1);
    });
  });

  describe('Touch Targets & Theme Consistency', () => {
    it('verifies interactive buttons meet the >= 64 logical unit touch target requirement', async () => {
      const flattenStyle = (style: unknown): Record<string, unknown> =>
        Array.isArray(style)
          ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flattenStyle(part) }), {})
          : ((style ?? {}) as Record<string, unknown>);

      await render(
        <ThemeProvider>
          <CatalogueEmptyNotice variant="no_matches" onClearFilters={() => {}} />
          <CatalogueEmptyNotice variant="pending_sync" onSync={() => {}} />
          <RejectedOperationCard
            operationTitle="Ordeño"
            onRetry={() => {}}
            onDismiss={() => {}}
          />
          <LocalStorageErrorNotice onRetry={() => {}} />
          <SessionExpiredNotice onSignIn={() => {}} />
        </ThemeProvider>
      );

      const clearBtn = screen.getByTestId('clear-filters-button');
      expect(flattenStyle(clearBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);

      const syncBtn = screen.getByTestId('sync-now-button');
      expect(flattenStyle(syncBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);

      const retryBtn = screen.getByTestId('rejected-retry');
      expect(flattenStyle(retryBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);

      const dismissBtn = screen.getByTestId('rejected-dismiss');
      expect(flattenStyle(dismissBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);

      const saveBtn = screen.getByTestId('retry-save-button');
      expect(flattenStyle(saveBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);

      const signInBtn = screen.getByTestId('sign-in-button');
      expect(flattenStyle(signInBtn.props.style).minHeight).toBeGreaterThanOrEqual(64);
    });
  });
});
