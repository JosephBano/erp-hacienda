# plan.md — Backup externo y recuperación

> Paquete completo: [tareas](./tasks.md) y [E2E](./test-e2e.md).
> Aplican [políticas](../../POLITICAS-OPERACION.md) y [preflight](../../PREFLIGHT-PRODUCCION.md).

> Ejecución futura del [spec](./spec.md). [Tareas](./tasks.md) y [E2E](./test-e2e.md) incluidos.

## Entregas secuenciales

1. `docs(ops): define encrypted offsite backup policy`.
   Aplicar ADR-0034 y ADR-0036 aceptados: rclone, custodia, retención, RPO/RTO y canal
   de alerta autohospedado. Confirmar destino de restore; verificar cuota real, consentimiento
   OAuth desatendido y cuenta bajo control. Actualizar `docs/BACKUPS.md` como runbook
   canónico cuando se implemente, sin afirmar automatización ya activa.
2. `fix(ops): make database backup and restore fail closed`.
   Modificar `scripts/backup.sh` y `scripts/restore.sh`; crear
   `scripts/backup-manifest.sh`. Formato custom, archivos temporales, checksums relativos,
   destino aislado obligatorio y ningún éxito ante error SQL. Verificar
   `bash -n scripts/backup.sh scripts/restore.sh` y restore real con PostgreSQL 16.
   Pruebas futuras en `tests/ops/backups/` cubren dump fallido, parciales, hash ausente,
   DB equivocada y restore con tabla corrupta; primero fallo reproducible, luego cambio.
3. `feat(ops): upload encrypted backups and rotate owned objects`.
   Crear `scripts/backup-upload.sh`, `scripts/backup-retention.sh` y
   `ops/backups/rclone.conf.example` sin secretos. Fixtures de IDs/edad/última copia y
   simulación de cuota/token/red. Ensayo real en carpeta Drive de prueba separada, con
   checksum tras descargar y evidencia de que no toca otros objetos del usuario.
4. `feat(ops): schedule backups and recovery monitoring`.
   Crear `ops/backups/hato-backup.service`, `.timer`,
   `ops/backups/hato-restore-check.service`, `.timer` y
   `scripts/backup-status.sh`. Usuario hato-backup, archivos protegidos, locks y heartbeat
   fuera de VPS. `systemd-analyze verify` sin errores; temporizadores muestran próximo
   disparo y Persistent; provocar fallo controlado y verificar recepción externa.
5. `docs(ops): record disaster recovery rehearsal`.
   Actualizar BACKUPS.md con comandos aprobados de exportación, reautorización, descarga,
   restore y validaciones de módulos; SEGURIDAD.md con custodia. Simulacro con credenciales
   recuperadas fuera de la VPS, medir RTO y ajustar capacidad/política si supera 4 horas.

## Dependencias y cierre

Cuenta y ADR → dump seguro → cifrado/subida → timers/alertas → restore externo → habilita
apertura de feature-0012. Puede desarrollarse con DB de prueba antes del VPS; la validación
final usa producción autorizada o su copia protegida, sin destruir fuente.

Ejecutar suite completa requerida por repositorio antes de PR y suite específica contra
PostgreSQL real. No usar InMemory para decidir recuperación. Documentar SHA, comandos,
resultados y copia restaurada. No publicar dumps ni secretos como artifacts de GitHub.

La poda de objetos es el punto irreversible: activar solo después de un dry-run revisado,
restore exitoso y prueba de namespace; nunca se aplica a historial de negocio ni carpetas
generales. PR explica propósito, políticas, restauración probada y exclusiones PITR/WORM.
