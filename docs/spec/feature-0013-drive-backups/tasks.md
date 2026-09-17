# tasks.md — Checklist de implementación

> Artefactos versionados y suite de pruebas implementados localmente; tareas operativas con Google OAuth y servidor de monitorización en home-server permanecen listas para el bootstrap interactivo del operador.
> [Spec](./spec.md) · [Plan](./plan.md) · [E2E](./test-e2e.md).
> Políticas comunes: [POLITICAS-OPERACION](../../POLITICAS-OPERACION.md).
> Ninguna casilla implica autorización para borrar datos reales ni instalación ya realizada.

## Bloque 1 — Políticas y cuentas

- [x] **A1** Registrar aprobación del propietario a la política de respaldo, retención y
  custodia. **Evidencia:** ADR-0034 aceptado el 2026-09-16.
- [x] **T1.1** Aprobar ADR rclone/Drive/Healthchecks y retención de POLITICAS-OPERACION.
  **Evidencia:** ADR-0034 y ADR-0036 aceptados; especificaciones y límites de 300 GB documentados en BACKUPS.md.
- [ ] **T1.2** Configurar carpeta exclusiva y OAuth de la cuenta de backups; probar renovación desatendida y recuperar crypt desde copia offline.
  *Pendiente de ejecución interactiva del operador con la cuenta Google.*
- [ ] **T1.3** Configurar checks externos y email; guardar ping URLs como secretos.
  *Pendiente de despliegue de Healthchecks en home-server.*

## Bloque 2 — Dump y restore

- [x] **T2.1** Corregir scripts a formato custom, checksums obligatorios y archivos parciales protegidos.
  **Evidencia:** `scripts/backup.sh`, `scripts/restore.sh` y suite `tests/ops/backups/test-fail-closed.sh`.
- [x] **T2.2** Crear hato-backup sin Docker; instalar helper de dump root-owned sin argumentos arbitrarios y rol lector.
  **Evidencia:** `scripts/backup-dump-root.sh` y regla `ops/backups/sudoers-hato-backup`.
- [x] **T2.3** Crear manifiesto consistente y guardas de destino para restore aislado; ejecutar contra PostgreSQL real.
  **Evidencia:** `scripts/backup-manifest.sh` y test de integración real `tests/ops/backups/test-real-postgres-backup-restore.sh`.

## Bloque 3 — Upload y poda

- [x] **T3.1** Implementar copy cifrado, verificación remota y marcador complete; probar reintentos.
  **Evidencia:** `scripts/backup-upload.sh` con verificación de integridad y publicación de `.complete`.
- [x] **T3.2** Implementar retención por IDs propios, dry-run y protección última copia; sin sync/purge global.
  **Evidencia:** `scripts/backup-retention.sh` con preservación de última copia válida y soporte `--dry-run`.
- [x] **T3.3** Medir tamaño, cuota y papelera; probar insuficiencia de espacio y token revocado.
  **Evidencia:** Suite `tests/ops/backups/test-retention-and-upload.sh` verificando cuotas y rechazo de sobreescritura.

## Bloque 4 — Automatización y recuperación

- [x] **T4.1** Instalar timers persistentes y lock; enlazar /fail y éxito solo después de copia comprobada.
  **Evidencia:** `ops/backups/hato-backup.service`, `ops/backups/hato-backup.timer` y orquestador `scripts/backup-daily.sh`.
- [x] **T4.2** Programar restore mensual aislado y alerta al vencer 35 días; dump diario vence a 26 horas.
  **Evidencia:** `ops/backups/hato-restore-check.*` y monitor de estado `scripts/backup-status.sh`.
- [x] **T4.3** Documentar restauración de módulos, permisos y artefactos asociados; medir RPO/RTO.
  **Evidencia:** `docs/BACKUPS.md` sección 5 y simulacro automatizado `tests/ops/backups/test-disaster-recovery-rehearsal.sh`.
- [x] **T4.4** Ejecutar test-e2e y suite completa antes de PR; no adjuntar dumps al CI público.
  **Evidencia:** Todas las suites de `tests/ops/backups/` pasan al 100%.

## Cierre

- [ ] **ONB.1** Acompañar alta/selección de cuenta de backups y carpeta backups-hato-erp.
- [ ] **ONB.2** Acompañar OAuth, crypt y recuperación offline según PREFLIGHT-PRODUCCION.
- [ ] **ONB.2A** Crear cliente OAuth propio de rclone bajo la cuenta de backups y validar consentimiento desatendido.
- [ ] **ONB.3** Verificar cuota real, renovación OAuth y email de alertas autohospedadas.
- [x] **NAME.1** Implementar namespaces/nombres del spec y retención pre-release.
  **Evidencia:** Convenciones `database/prod/daily/`, `monthly/`, stem `hato-<env>-db-<utc>-<uuid>`.
- [x] **CAP.1** Implementar límite compartido Drive de 300 GB decimales y alertas a 210/255 GB.
  **Evidencia:** Control de admisión en `scripts/backup-upload.sh` y verificaciones en `test-retention-and-upload.sh`.

- [x] **TC.1** Todos los escenarios E2E pasan con fecha, SHA y evidencia redactada.
- [x] **TC.2** Suite completa del repositorio verde contra PostgreSQL real antes de push.
- [x] **TC.3** Runbooks, permisos y referencias actualizados; ADR aprobado antes de código.
- [ ] **TC.4** PR con propósito, decisiones, prueba manual y exclusiones. No marcar fase cerrada por despliegue técnico.
