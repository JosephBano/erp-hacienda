import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { useTheme } from './theme';

// -----------------------------------------------------------------------------
// Helper: Error Translation & Sanitization (T3.5, T3.7)
// -----------------------------------------------------------------------------

/**
 * Traduce y sanea mensajes de error del servidor a lenguaje claro para el empleado de campo.
 * Elimina jerga técnica como cursores de sincronización, UUIDs crudos y nombres de tablas de base de datos.
 */
export function formatOperationError(errorDetails?: string | null): string {
  if (!errorDetails || !errorDetails.trim()) {
    return 'El servidor rechazó la operación sin indicar el motivo.';
  }

  const raw = errorDetails.trim();

  // Dependencia de parto rechazado
  if (/depende de un nacimiento rechazado/i.test(raw)) {
    const innerPart = raw.replace(/^depende de un nacimiento rechazado:?\s*/i, '').trim();
    if (innerPart) {
      const cleanInner = formatOperationError(innerPart);
      return `Esta operación depende de un parto rechazado por el servidor: ${cleanInner}`;
    }
    return 'Esta operación depende de un parto que fue rechazado por el servidor.';
  }

  // Errores de red y conectividad
  if (/network request failed|failed to fetch|econnrefused|sockettimeout|timed out|enotfound/i.test(raw)) {
    return 'Sin conexión con el servidor. El registro está guardado en este teléfono y se enviará cuando haya señal.';
  }

  // Sesión y autenticación
  if (/\b401\b|unauthorized|jwt expired|token expired|token inv[aá]lido|sesi[oó]n expirada|sesi[oó]n caduc/i.test(raw)) {
    return 'La sesión caducó. Sus registros locales están a salvo. Inicie sesión para reanudar el envío.';
  }

  // Permisos y autorización
  if (/\b403\b|permission denied|permissiondenied|lacks permission|forbidden|no tiene permiso|no autorizado|acceso denegado/i.test(raw)) {
    return 'No tiene permisos para registrar esta actividad en el sistema.';
  }

  // Incompatibilidad por sexo o aptitud biológica
  if (
    /(male|macho).*(milking|ordeño)|(milking|ordeño).*(male|macho)|(male|macho).*(birth|parto)|(birth|parto).*(male|macho)|incompatible.*sex|no apto por sexo|sexo no compatible|parto solo aplica a hembras/i.test(
      raw
    )
  ) {
    return 'El animal seleccionado no es apto para esta actividad (sexo o condición biológica no compatible).';
  }

  // Período de retiro activo
  if (/withdrawal|per[ií]odo de retiro/i.test(raw)) {
    return 'El animal se encuentra bajo período de retiro activo.';
  }

  // Animal fallecido o dado de baja
  if (/deceased|culled|dado de baja|fallecido|muerto/i.test(raw)) {
    return 'El animal se encuentra registrado como fallecido o dado de baja.';
  }

  // Duplicados o conflicto de clave única
  if (/duplicate|already exists|unique constraint|ya existe|ya asignado|identificador duplicado|arete duplicado/i.test(raw)) {
    return 'El número de arete o identificador ya está asignado a otro animal.';
  }

  // Conflicto de concurrencia
  if (/concurrency|optimistic|conflict|conflicto de versi[oó]n/i.test(raw)) {
    return 'El registro fue modificado previamente en el servidor. Revise los datos e intente de nuevo.';
  }

  // Sujeto no encontrado (en inglés técnico)
  if (/animal.*not found|group.*not found|lot.*not found|no se encontr[oó] el animal/i.test(raw)) {
    return 'El animal no existe o no fue encontrado en el servidor.';
  }

  // Excepciones técnicas del servidor (stack traces, SQL, Npgsql, 500)
  if (/Exception\b|PostgresException|Npgsql|SqlException|System\.|Error 500\b|Internal Server Error/i.test(raw)) {
    return 'Ocurrió un error en el servidor al procesar la solicitud. El registro se conserva intacto en el teléfono.';
  }

  // Si ya es un mensaje legible en español sin jerga técnica, sanear UUIDs, cursores y nombres de tablas
  let cleaned = raw;

  // 1. Cursores
  cleaned = cleaned.replace(/invalid sync cursor(\s+[0-9a-fA-F-]+)?/gi, 'error de sincronización');
  cleaned = cleaned.replace(/cursor[:=\s]+[0-9a-zA-Z_-]+/gi, '');
  cleaned = cleaned.replace(/\bcursor\b/gi, 'punto de sincronización');

  // 2. Nombres de tablas de base de datos y preposiciones asociadas
  cleaned = cleaned.replace(
    /\b(en la tabla|en tabla|de la tabla|in table|in\s+|en\s+|table\s*)['"]?(sync_outbox|milk_yields|animal_events|birth_events|animals|animal_groups|treatments|medications)['"]?/gi,
    ''
  );
  cleaned = cleaned.replace(
    /\b(sync_outbox|milk_yields|animal_events|birth_events|animals|animal_groups|treatments|medications)\b/gi,
    ''
  );

  // 3. UUIDs crudos (formato 8-4-4-4-12)
  cleaned = cleaned.replace(/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/g, '');

  // 4. Limpieza de puntuación y preposiciones huérfanas
  cleaned = cleaned.replace(/\s+/g, ' ');
  cleaned = cleaned.replace(/\s+([.,;:])/g, '$1');
  cleaned = cleaned.replace(/^[.,;:\s-]+/, '');
  cleaned = cleaned.replace(/\s+\b(in|en|de|para|for|at)\b\s*$/i, '');
  cleaned = cleaned.trim();

  if (!cleaned || /^[^a-zA-Z0-9áéíóúÁÉÍÓÚñÑ]+$/.test(cleaned)) {
    return 'El servidor rechazó la operación sin indicar el motivo.';
  }

  return cleaned;
}

// -----------------------------------------------------------------------------
// 1. RecordStatusBadge
// -----------------------------------------------------------------------------

export type RecordStatus =
  | 'local_pending'
  | 'syncing'
  | 'accepted_pull_pending'
  | 'synced'
  | 'rejected'
  | 'error';

export interface RecordStatusBadgeProps {
  status: RecordStatus;
  label?: string;
  size?: 'normal' | 'large';
  testID?: string;
  accessibilityLabel?: string;
}

/**
 * Píldora de estado con lenguaje claro y alto contraste para los registros.
 * Estados:
 *  - local_pending: "Guardado local" (Sin red, guardado en teléfono)
 *  - syncing: "Enviando..." (Envío o descarga en progreso)
 *  - accepted_pull_pending: "Aceptado (descarga pendiente)" (Registro aceptado en servidor, delta pendiente)
 *  - synced: "Al día" (Completamente sincronizado)
 *  - rejected: "Rechazado" (Rechazado por validación/regla de servidor)
 *  - error: "Error local" (Fallo de persistencia local)
 */
export function RecordStatusBadge({
  status,
  label,
  size = 'normal',
  testID,
  accessibilityLabel,
}: RecordStatusBadgeProps) {
  const { theme: activeTheme } = useTheme();

  const config = React.useMemo(() => {
    switch (status) {
      case 'local_pending':
      case 'pending':
        return {
          defaultLabel: 'Guardado local',
          defaultA11y: 'Guardado en este teléfono. Pendiente de enviar. Puede seguir trabajando.',
          bg: activeTheme.color.warning,
          textColor: activeTheme.color.warningText,
          borderColor: 'transparent',
        };
      case 'syncing':
        return {
          defaultLabel: 'Enviando...',
          defaultA11y: 'Envío en curso al servidor. La captura local sigue disponible.',
          bg: activeTheme.color.info,
          textColor: '#FFFFFF',
          borderColor: 'transparent',
        };
      case 'accepted_pull_pending':
        return {
          defaultLabel: 'Aceptado (descarga pendiente)',
          defaultA11y: 'Ese registro llegó al servidor. Todavía faltan cambios por recibir en el teléfono.',
          bg: activeTheme.color.surfaceRaised,
          textColor: activeTheme.color.text,
          borderColor: activeTheme.color.border,
        };
      case 'synced':
        return {
          defaultLabel: 'Al día',
          defaultA11y: 'Registro confirmado y sincronizado con el servidor.',
          bg: activeTheme.color.primary,
          textColor: activeTheme.color.primaryText,
          borderColor: 'transparent',
        };
      case 'rejected':
        return {
          defaultLabel: 'Rechazado',
          defaultA11y: 'Operación rechazada por el servidor.',
          bg: activeTheme.color.danger,
          textColor: activeTheme.color.dangerText,
          borderColor: 'transparent',
        };
      case 'error':
      case 'cancelled':
      default:
        return {
          defaultLabel: 'Error local',
          defaultA11y: 'Error de almacenamiento local. No se pudo guardar en el teléfono.',
          bg: activeTheme.color.danger,
          textColor: activeTheme.color.dangerText,
          borderColor: 'transparent',
        };
    }
  }, [status, activeTheme]);

  const displayLabel = label ?? config.defaultLabel;
  const isLarge = size === 'large';

  return (
    <View
      testID={testID}
      accessibilityRole="text"
      accessibilityLabel={accessibilityLabel ?? config.defaultA11y}
      style={[
        styles.badge,
        {
          backgroundColor: config.bg,
          borderColor: config.borderColor,
          paddingHorizontal: isLarge ? activeTheme.space.md : activeTheme.space.sm,
          paddingVertical: isLarge ? activeTheme.space.xs + 2 : activeTheme.space.xs,
          borderRadius: activeTheme.radius.md,
        },
      ]}
    >
      <Text
        style={[
          styles.badgeText,
          {
            color: config.textColor,
            fontSize: isLarge ? activeTheme.font.body : activeTheme.font.micro,
            fontWeight: '700',
          },
        ]}
      >
        {displayLabel}
      </Text>
    </View>
  );
}

// -----------------------------------------------------------------------------
// 2. SyncStateNotice
// -----------------------------------------------------------------------------

export type SyncState =
  | 'offline_pending'
  | 'pushing'
  | 'pulling'
  | 'syncing'
  | 'auth_expired'
  | 'error'
  | 'synced';

export interface SyncStateNoticeProps {
  state: SyncState;
  pendingCount?: number;
  errorMessage?: string;
  onAction?: () => void;
  actionLabel?: string;
  onDiagnostic?: () => void;
  diagnosticLabel?: string;
  testID?: string;
}

/**
 * Aviso global de estado de sincronización.
 * Garantiza que "Sin señal" no se presente como fracaso (D5) y mantiene
 * al operador informado de si puede continuar registrando en el galpón.
 */
export function SyncStateNotice({
  state,
  pendingCount,
  errorMessage,
  onAction,
  actionLabel,
  onDiagnostic,
  diagnosticLabel = 'Compartir diagnóstico',
  testID,
}: SyncStateNoticeProps) {
  const { theme: activeTheme } = useTheme();

  const details = React.useMemo(() => {
    switch (state) {
      case 'offline_pending': {
        const countText =
          pendingCount !== undefined && pendingCount > 0
            ? pendingCount === 1
              ? '1 registro guardado'
              : `${pendingCount} registros guardados`
            : 'Guardado';
        return {
          title: 'Guardado en este teléfono',
          message: `${countText} en este teléfono. Pendiente de enviar. Puede seguir trabajando.`,
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.warning,
          textColor: activeTheme.color.text,
          tone: 'warning' as const,
        };
      }
      case 'pushing':
        return {
          title: 'Enviando registros',
          message: 'Enviando registros al servidor... La captura local sigue disponible.',
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.info,
          textColor: activeTheme.color.text,
          tone: 'info' as const,
        };
      case 'pulling':
        return {
          title: 'Descargando datos',
          message: 'Descargando datos desde la oficina... La captura local sigue disponible.',
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.info,
          textColor: activeTheme.color.text,
          tone: 'info' as const,
        };
      case 'syncing':
        return {
          title: 'Sincronizando',
          message: 'Sincronización en curso con el servidor... La captura local sigue disponible.',
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.info,
          textColor: activeTheme.color.text,
          tone: 'info' as const,
        };
      case 'auth_expired':
        return {
          title: 'Sesión expirada',
          message: 'Sesión expirada. Su trabajo está guardado localmente. Inicie sesión para reanudar el envío.',
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.warning,
          textColor: activeTheme.color.text,
          tone: 'warning' as const,
        };
      case 'error':
        return {
          title: 'Sincronización pendiente',
          message: errorMessage
            ? formatOperationError(errorMessage)
            : 'No se pudo sincronizar con el servidor. Sus registros locales están a salvo y se reintentará automáticamente.',
          bg: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.warning,
          textColor: activeTheme.color.text,
          tone: 'warning' as const,
        };
      case 'synced':
        return {
          title: 'Sincronización al día',
          message: 'Todo al día. No hay registros pendientes de enviar.',
          bg: activeTheme.color.surface,
          borderColor: activeTheme.color.primary,
          textColor: activeTheme.color.text,
          tone: 'neutral' as const,
        };
    }
  }, [state, pendingCount, errorMessage, activeTheme]);

  return (
    <View
      testID={testID}
      accessibilityRole="alert"
      style={[
        styles.noticeContainer,
        {
          backgroundColor: details.bg,
          borderColor: details.borderColor,
          borderRadius: activeTheme.radius.md,
          padding: activeTheme.space.md,
          gap: activeTheme.space.sm,
        },
      ]}
    >
      <View style={styles.noticeHeader}>
        <Text style={[styles.noticeTitle, { color: details.textColor, fontSize: activeTheme.font.body }]}>
          {details.title}
        </Text>
        {(state === 'pushing' || state === 'pulling' || state === 'syncing') && (
          <ActivityIndicator size="small" color={activeTheme.color.info} />
        )}
      </View>

      <Text style={[styles.noticeBody, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
        {details.message}
      </Text>

      {onAction && actionLabel ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={actionLabel}
          onPress={onAction}
          style={({ pressed }) => [
            styles.actionButton,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: activeTheme.color.surface,
              borderColor: activeTheme.color.border,
              borderRadius: activeTheme.radius.md,
              opacity: pressed ? 0.8 : 1,
            },
          ]}
        >
          <Text style={[styles.actionButtonText, { color: activeTheme.color.text, fontSize: activeTheme.font.body }]}>
            {actionLabel}
          </Text>
        </Pressable>
      ) : null}

      {onDiagnostic ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={diagnosticLabel}
          onPress={onDiagnostic}
          style={({ pressed }) => [
            styles.actionButton,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: 'transparent',
              borderColor: activeTheme.color.border,
              borderRadius: activeTheme.radius.md,
              opacity: pressed ? 0.8 : 1,
            },
          ]}
        >
          <Text style={[styles.actionButtonText, { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label }]}>
            {diagnosticLabel}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// -----------------------------------------------------------------------------
// 3. CatalogueEmptyNotice (T3.4)
// -----------------------------------------------------------------------------

export type CatalogueEmptyVariant = 'empty' | 'no_matches' | 'pending_sync';

export interface CatalogueEmptyNoticeProps {
  variant: CatalogueEmptyVariant;
  title?: string;
  message?: string;
  onSync?: () => void;
  syncLabel?: string;
  onClearFilters?: () => void;
  clearFiltersLabel?: string;
  action?: { label: string; onPress: () => void };
  testID?: string;
}

/**
 * Mensaje para catálogos vacíos o incompletos.
 * Distingue explícitamente entre tres escenarios distintos (T3.4):
 *  1. 'empty': Ausencia real de registros en la finca ("No hay registros").
 *  2. 'no_matches': Filtro o búsqueda sin resultados ("Ningún resultado coincide con la búsqueda").
 *  3. 'pending_sync': Datos aún no descargados del servidor ("Faltan datos por sincronizar desde la oficina").
 */
export function CatalogueEmptyNotice({
  variant,
  title,
  message,
  onSync,
  syncLabel = 'Sincronizar ahora',
  onClearFilters,
  clearFiltersLabel = 'Limpiar filtros',
  action,
  testID,
}: CatalogueEmptyNoticeProps) {
  const { theme: activeTheme } = useTheme();

  const defaults = React.useMemo(() => {
    switch (variant) {
      case 'empty':
        return {
          title: 'No hay registros',
          message: 'No existen registros en la finca para esta sección.',
          actionButton: action,
        };
      case 'no_matches':
        return {
          title: 'Ningún resultado coincide con la búsqueda',
          message: 'Pruebe con otros términos o limpie los filtros seleccionados.',
          actionButton: onClearFilters
            ? { label: clearFiltersLabel, onPress: onClearFilters }
            : action,
        };
      case 'pending_sync':
        return {
          title: 'Faltan datos por sincronizar desde la oficina',
          message: 'Es posible que la información aún no se haya descargado en este teléfono.',
          actionButton: onSync ? { label: syncLabel, onPress: onSync } : action,
        };
    }
  }, [variant, onSync, syncLabel, onClearFilters, clearFiltersLabel, action]);

  const resolvedTitle = title ?? defaults.title;
  const resolvedMessage = message ?? defaults.message;
  const activeAction = action ?? defaults.actionButton;

  return (
    <View
      testID={testID}
      style={[
        styles.emptyNoticeContainer,
        {
          backgroundColor: activeTheme.color.surface,
          borderColor: activeTheme.color.border,
          borderRadius: activeTheme.radius.md,
          padding: activeTheme.space.lg,
          gap: activeTheme.space.md,
        },
      ]}
    >
      <View style={styles.emptyNoticeTextGroup}>
        <Text
          style={[
            styles.emptyNoticeTitle,
            { color: activeTheme.color.text, fontSize: activeTheme.font.subtitle },
          ]}
        >
          {resolvedTitle}
        </Text>
        <Text
          style={[
            styles.emptyNoticeMessage,
            { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label },
          ]}
        >
          {resolvedMessage}
        </Text>
      </View>

      {activeAction ? (
        <Pressable
          testID={
            variant === 'no_matches'
              ? 'clear-filters-button'
              : variant === 'pending_sync'
                ? 'sync-now-button'
                : 'catalogue-action-button'
          }
          accessibilityRole="button"
          accessibilityLabel={activeAction.label}
          onPress={activeAction.onPress}
          style={({ pressed }) => [
            styles.actionButton,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: activeTheme.color.surfaceRaised,
              borderColor: activeTheme.color.border,
              borderRadius: activeTheme.radius.md,
              opacity: pressed ? 0.8 : 1,
            },
          ]}
        >
          <Text
            style={[
              styles.actionButtonText,
              { color: activeTheme.color.text, fontSize: activeTheme.font.body },
            ]}
          >
            {activeAction.label}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// -----------------------------------------------------------------------------
// 4. RejectedOperationCard (T3.5)
// -----------------------------------------------------------------------------

export interface RejectedOperationCardProps {
  operationTitle: string;
  occurredAt?: string | number | Date;
  errorDetails?: string;
  payload?: Record<string, unknown> | string;
  onRetry?: () => void;
  retryLabel?: string;
  onDismiss?: () => void;
  dismissLabel?: string;
  onDiagnostic?: () => void;
  diagnosticLabel?: string;
  testID?: string;
}

/**
 * Tarjeta de operación rechazada.
 * Muestra:
 *  - Qué registro fue rechazado y fecha/hora
 *  - Motivo en lenguaje legible (sanitizado de cursores, UUIDs y tablas)
 *  - Acciones concretas disponibles (corregir/reintentar, descartar)
 *  - Contenido original conservado visiblemente para no perder datos del empleado.
 */
export function RejectedOperationCard({
  operationTitle,
  occurredAt,
  errorDetails,
  payload,
  onRetry,
  retryLabel = 'Corregir o reintentar',
  onDismiss,
  dismissLabel = 'Descartar',
  onDiagnostic,
  diagnosticLabel = 'Ver diagnóstico técnico',
  testID,
}: RejectedOperationCardProps) {
  const { theme: activeTheme } = useTheme();

  const formattedReason = React.useMemo(() => {
    return formatOperationError(errorDetails);
  }, [errorDetails]);

  const formattedDate = React.useMemo(() => {
    if (!occurredAt) return undefined;
    try {
      const d = new Date(occurredAt);
      return isNaN(d.getTime()) ? String(occurredAt) : d.toLocaleString();
    } catch {
      return String(occurredAt);
    }
  }, [occurredAt]);

  const payloadEntries = React.useMemo(() => {
    if (!payload) return null;
    if (typeof payload === 'string') return payload;
    try {
      const entries = Object.entries(payload).filter(([k]) => !k.startsWith('_'));
      return entries.length > 0 ? entries : null;
    } catch {
      return null;
    }
  }, [payload]);

  return (
    <View
      testID={testID}
      style={[
        styles.rejectedCard,
        {
          backgroundColor: activeTheme.color.surface,
          borderColor: activeTheme.color.danger,
          borderRadius: activeTheme.radius.md,
          padding: activeTheme.space.md,
          gap: activeTheme.space.sm,
        },
      ]}
    >
      <View style={styles.rejectedCardHeader}>
        <View style={{ flex: 1, gap: 2 }}>
          <Text
            style={[
              styles.rejectedTitle,
              { color: activeTheme.color.text, fontSize: activeTheme.font.subtitle },
            ]}
          >
            {operationTitle}
          </Text>
          {formattedDate ? (
            <Text
              style={[
                styles.rejectedDate,
                { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label },
              ]}
            >
              {formattedDate}
            </Text>
          ) : null}
        </View>
        <RecordStatusBadge status="rejected" />
      </View>

      {/* Motivo legible */}
      <View
        testID="rejected-reason"
        style={[
          styles.reasonBox,
          {
            backgroundColor: activeTheme.color.surfaceRaised,
            borderColor: activeTheme.color.border,
            borderRadius: activeTheme.radius.md,
            padding: activeTheme.space.sm,
          },
        ]}
      >
        <Text
          style={[
            styles.reasonLabel,
            { color: activeTheme.color.danger, fontSize: activeTheme.font.micro },
          ]}
        >
          Motivo del rechazo:
        </Text>
        <Text
          testID="rejected-reason-text"
          style={[
            styles.reasonText,
            { color: activeTheme.color.text, fontSize: activeTheme.font.label },
          ]}
        >
          {formattedReason}
        </Text>
      </View>

      {/* Contenido conservado (Payload) */}
      <View
        testID="rejected-payload"
        style={[
          styles.payloadBox,
          {
            backgroundColor: activeTheme.color.surfaceRaised,
            borderColor: activeTheme.color.border,
            borderRadius: activeTheme.radius.md,
            padding: activeTheme.space.sm,
            gap: 4,
          },
        ]}
      >
        <Text
          style={[
            styles.payloadLabel,
            { color: activeTheme.color.textMuted, fontSize: activeTheme.font.micro },
          ]}
        >
          Datos conservados en el teléfono:
        </Text>
        {Array.isArray(payloadEntries) ? (
          payloadEntries.map(([key, val]) => (
            <View key={key} style={styles.payloadRow}>
              <Text
                style={[
                  styles.payloadKey,
                  { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label },
                ]}
              >
                {`${key}: `}
              </Text>
              <Text
                style={[
                  styles.payloadValue,
                  { color: activeTheme.color.text, fontSize: activeTheme.font.label },
                ]}
              >
                {typeof val === 'object' ? JSON.stringify(val) : String(val)}
              </Text>
            </View>
          ))
        ) : (
          <Text
            testID="rejected-payload-text"
            style={[
              styles.payloadValue,
              { color: activeTheme.color.text, fontSize: activeTheme.font.label },
            ]}
          >
            {typeof payloadEntries === 'string'
              ? payloadEntries
              : 'El contenido completo de este registro está intacto.'}
          </Text>
        )}
      </View>

      {/* Acciones disponibles con touchTarget >= 64 */}
      <View style={[styles.actionsRow, { gap: activeTheme.space.sm, marginTop: activeTheme.space.xs }]}>
        {onRetry ? (
          <Pressable
            testID="rejected-retry"
            accessibilityRole="button"
            accessibilityLabel={retryLabel}
            onPress={onRetry}
            style={({ pressed }) => [
              styles.actionButton,
              {
                minHeight: activeTheme.touchTarget,
                backgroundColor: activeTheme.color.primary,
                borderRadius: activeTheme.radius.md,
                opacity: pressed ? 0.8 : 1,
              },
            ]}
          >
            <Text
              style={[
                styles.actionButtonText,
                { color: activeTheme.color.primaryText, fontSize: activeTheme.font.body },
              ]}
            >
              {retryLabel}
            </Text>
          </Pressable>
        ) : null}

        {onDismiss ? (
          <Pressable
            testID="rejected-dismiss"
            accessibilityRole="button"
            accessibilityLabel={dismissLabel}
            onPress={onDismiss}
            style={({ pressed }) => [
              styles.actionButton,
              {
                minHeight: activeTheme.touchTarget,
                backgroundColor: activeTheme.color.surfaceRaised,
                borderColor: activeTheme.color.border,
                borderRadius: activeTheme.radius.md,
                opacity: pressed ? 0.8 : 1,
              },
            ]}
          >
            <Text
              style={[
                styles.actionButtonText,
                { color: activeTheme.color.text, fontSize: activeTheme.font.body },
              ]}
            >
              {dismissLabel}
            </Text>
          </Pressable>
        ) : null}

        {onDiagnostic ? (
          <Pressable
            testID="rejected-diagnostic"
            accessibilityRole="button"
            accessibilityLabel={diagnosticLabel}
            onPress={onDiagnostic}
            style={({ pressed }) => [
              styles.actionButton,
              {
                minHeight: activeTheme.touchTarget,
                backgroundColor: 'transparent',
                borderColor: activeTheme.color.border,
                borderRadius: activeTheme.radius.md,
                opacity: pressed ? 0.8 : 1,
              },
            ]}
          >
            <Text
              style={[
                styles.actionButtonText,
                { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label },
              ]}
            >
              {diagnosticLabel}
            </Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

// -----------------------------------------------------------------------------
// 5. LocalStorageErrorNotice (T3.3)
// -----------------------------------------------------------------------------

export interface LocalStorageErrorNoticeProps {
  errorMessage?: string;
  onRetry?: () => void;
  retryLabel?: string;
  testID?: string;
}

/**
 * Aviso ante un error de almacenamiento local en el dispositivo.
 * Reglas duras (T3.3):
 *  - NUNCA afirma «guardado».
 *  - NUNCA sugiere borrar la app ni reinstalarla.
 *  - Instruye conservar los datos en pantalla y reintentar.
 */
export function LocalStorageErrorNotice({
  errorMessage,
  onRetry,
  retryLabel = 'Reintentar guardado',
  testID,
}: LocalStorageErrorNoticeProps) {
  const { theme: activeTheme } = useTheme();

  return (
    <View
      testID={testID}
      accessibilityRole="alert"
      style={[
        styles.storageErrorContainer,
        {
          backgroundColor: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.danger,
          borderRadius: activeTheme.radius.md,
          padding: activeTheme.space.md,
          gap: activeTheme.space.sm,
        },
      ]}
    >
      <Text
        style={[
          styles.storageErrorTitle,
          { color: activeTheme.color.danger, fontSize: activeTheme.font.subtitle },
        ]}
      >
        Error de almacenamiento local
      </Text>

      <Text
        style={[
          styles.storageErrorBody,
          { color: activeTheme.color.text, fontSize: activeTheme.font.body },
        ]}
      >
        No se pudo guardar el registro en este teléfono. Conserve los datos en pantalla e intente guardar nuevamente. No cierre ni desinstale la aplicación para evitar perder su información.
      </Text>

      {errorMessage ? (
        <Text
          style={[
            styles.storageErrorDetail,
            { color: activeTheme.color.textMuted, fontSize: activeTheme.font.label },
          ]}
        >
          {formatOperationError(errorMessage)}
        </Text>
      ) : null}

      {onRetry ? (
        <Pressable
          testID="retry-save-button"
          accessibilityRole="button"
          accessibilityLabel={retryLabel}
          onPress={onRetry}
          style={({ pressed }) => [
            styles.actionButton,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: activeTheme.color.danger,
              borderRadius: activeTheme.radius.md,
              opacity: pressed ? 0.8 : 1,
              marginTop: activeTheme.space.xs,
            },
          ]}
        >
          <Text
            style={[
              styles.actionButtonText,
              { color: activeTheme.color.dangerText, fontSize: activeTheme.font.body },
            ]}
          >
            {retryLabel}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// -----------------------------------------------------------------------------
// 6. SessionExpiredNotice (T3.6)
// -----------------------------------------------------------------------------

export interface SessionExpiredNoticeProps {
  onSignIn?: () => void;
  signInLabel?: string;
  testID?: string;
}

/**
 * Aviso de sesión expirada (T3.6).
 * Explica con total claridad:
 *  - Su trabajo está guardado localmente en este teléfono.
 *  - Cómo recuperar el envío: iniciar sesión para reanudar.
 */
export function SessionExpiredNotice({
  onSignIn,
  signInLabel = 'Iniciar sesión para reanudar el envío',
  testID,
}: SessionExpiredNoticeProps) {
  const { theme: activeTheme } = useTheme();

  return (
    <View
      testID={testID}
      accessibilityRole="alert"
      style={[
        styles.sessionExpiredContainer,
        {
          backgroundColor: activeTheme.color.surfaceRaised,
          borderColor: activeTheme.color.warning,
          borderRadius: activeTheme.radius.md,
          padding: activeTheme.space.md,
          gap: activeTheme.space.sm,
        },
      ]}
    >
      <Text
        style={[
          styles.sessionExpiredTitle,
          { color: activeTheme.color.warning, fontSize: activeTheme.font.subtitle },
        ]}
      >
        Sesión expirada
      </Text>

      <Text
        style={[
          styles.sessionExpiredBody,
          { color: activeTheme.color.text, fontSize: activeTheme.font.body },
        ]}
      >
        Sesión expirada. Su trabajo está guardado localmente en este teléfono. Inicie sesión para reanudar el envío al servidor.
      </Text>

      {onSignIn ? (
        <Pressable
          testID="sign-in-button"
          accessibilityRole="button"
          accessibilityLabel={signInLabel}
          onPress={onSignIn}
          style={({ pressed }) => [
            styles.actionButton,
            {
              minHeight: activeTheme.touchTarget,
              backgroundColor: activeTheme.color.primary,
              borderRadius: activeTheme.radius.md,
              opacity: pressed ? 0.8 : 1,
              marginTop: activeTheme.space.xs,
            },
          ]}
        >
          <Text
            style={[
              styles.actionButtonText,
              { color: activeTheme.color.primaryText, fontSize: activeTheme.font.body },
            ]}
          >
            {signInLabel}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

// -----------------------------------------------------------------------------
// Styles
// -----------------------------------------------------------------------------

const styles = StyleSheet.create({
  badge: {
    borderWidth: 1,
    alignSelf: 'flex-start',
    justifyContent: 'center',
    alignItems: 'center',
  },
  badgeText: {
    letterSpacing: 0.3,
  },
  noticeContainer: {
    borderWidth: 1,
  },
  noticeHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  noticeTitle: {
    fontWeight: '800',
  },
  noticeBody: {
    fontWeight: '500',
  },
  actionButton: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
    borderWidth: 1,
  },
  actionButtonText: {
    fontWeight: '700',
    textAlign: 'center',
  },
  emptyNoticeContainer: {
    borderWidth: 1,
    borderStyle: 'dashed',
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyNoticeTextGroup: {
    alignItems: 'center',
    gap: 6,
  },
  emptyNoticeTitle: {
    fontWeight: '700',
    textAlign: 'center',
  },
  emptyNoticeMessage: {
    textAlign: 'center',
  },
  rejectedCard: {
    borderWidth: 1.5,
  },
  rejectedCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: 8,
  },
  rejectedTitle: {
    fontWeight: '800',
  },
  rejectedDate: {
    fontWeight: '500',
  },
  reasonBox: {
    borderWidth: 1,
    gap: 2,
  },
  reasonLabel: {
    fontWeight: '700',
    textTransform: 'uppercase',
  },
  reasonText: {
    fontWeight: '600',
  },
  payloadBox: {
    borderWidth: 1,
  },
  payloadLabel: {
    fontWeight: '700',
  },
  payloadRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  payloadKey: {
    fontWeight: '600',
  },
  payloadValue: {
    fontWeight: '500',
  },
  actionsRow: {
    flexDirection: 'column',
  },
  storageErrorContainer: {
    borderWidth: 2,
  },
  storageErrorTitle: {
    fontWeight: '800',
  },
  storageErrorBody: {
    fontWeight: '600',
  },
  storageErrorDetail: {
    fontStyle: 'italic',
  },
  sessionExpiredContainer: {
    borderWidth: 1.5,
  },
  sessionExpiredTitle: {
    fontWeight: '800',
  },
  sessionExpiredBody: {
    fontWeight: '600',
  },
});
