# BACKUPS.md — Estrategia y Runbook de Backups Cifrados y Recuperación (Art. 2)

> **Constitución Art. 2**: *"Los backups deben estar automatizados y probarse periódicamente con una
> restauración real; un backup no probado es un deseo, no una copia de seguridad."*
>
> **Decisiones aplicadas:** [ADR-0034](./adr/0034-backups-cifrados-drive.md) (Backups cifrados Drive)
> y [ADR-0036](./adr/0036-monitorizacion-autohospedada.md) (Monitorización autohospedada en home-server).
> Especificación técnica: [feature-0013](./spec/feature-0013-drive-backups/spec.md).
> Políticas comunes: [POLITICAS-OPERACION.md](./POLITICAS-OPERACION.md).
>
> **Estado de implementación:** Los scripts, contratos de ejecución, tests automatizados y unidades systemd
> están implementados y versionados en este repositorio. Su puesta en marcha en la VPS de producción requiere
> completar el bootstrap de cuentas y secretos según [PREFLIGHT-PRODUCCION.md](./PREFLIGHT-PRODUCCION.md).

---

## 1. Arquitectura y Principios de Diseño

1. **Inmutabilidad y Cifrado Offsite**: Exportación en formato custom de PostgreSQL (`pg_dump -Fc --no-owner --no-acl`),
   con nombres y contenido cifrados mediante `rclone crypt` hacia Google Drive dedicado (`backups-hato-erp/`).
2. **Fail-Closed Estricto (Fallo Cerrado)**:
   - Todo error en dump, cálculo de hash, subida o restauración aborta con código de salida distinto de cero.
   - Prohibido cualquier mensaje de éxito si ocurrió un fallo SQL o de red.
   - Archivos parciales (`.partial`) jamás se marcan como válidos ni se publican en remoto.
   - Marcador `.complete` y ping de éxito a monitor solo tras verificación remota exhaustiva.
3. **Mínimo Privilegio Operativo**:
   - Usuario de servicio local `hato-backup` sin permisos `sudo` generales y **sin pertenencia al grupo `docker`**.
   - Acceso a base de datos mediante rol de sólo lectura (`PRODUCTION_POSTGRES_BACKUP_USER`) y helper root-owned
     (`/usr/local/libexec/hato/backup-dump-root`) sin argumentos arbitrarios.
4. **Almacenamiento Compartido y Cuota Acotada**:
   - Límite operativo aprobado de **300 GB decimales (300 000 000 000 bytes)** en carpeta Drive compartida con APKs y logs.
   - Umbrales de alerta: advertencia a **210 GB (70 %)** y alerta urgente a **255 GB (85 %)**.
   - Control de admisión previo a cada subida (evalúa tamaño estimado, cuota libre de cuenta y papelera).
5. **Retención Segura**:
   - **Local:** 7 días.
   - **Drive Diarios:** 30 días.
   - **Drive Mensuales:** 1 copia mensual conservada por 12 meses.
   - **Regla de Oro:** Nunca borrar la última copia válida verificada por edad.
   - La rotación evalúa manifiestos e identificadores propios (`UUID`), jamás usa `sync`, `mirror` ni `purge` global.
6. **Objetivos RPO y RTO**:
   - **RPO Objetivo:** 24 horas (respaldo diario a 07:00 UTC / 02:00 Ecuador; backup adicional pre-migración).
   - **RTO Objetivo:** 4 horas (medido en simulacro de restauración completa en host limpio).

---

## 2. Nombres, Manifiesto y Namespaces

### Estructura de Namespaces en Drive (vista lógica descifrada)
```
backups-hato-erp/
├── database/
│   └── prod/
│       ├── daily/
│       ├── monthly/
│       └── pre-release/
├── mobile/
│   ├── stage/
│   └── prod/
└── monitoring/
    └── daily/
```
*Nota: En Google Drive los nombres y contenidos se almacenan cifrados con nombres opacos.*

### Convención de Archivos
Cada ejecución genera cuatro archivos complementarios con el mismo prefijo:
- `hato-prod-db-<TIMESTAMP>Z-<UUID>.dump` (dump binario PostgreSQL custom comprimido)
- `hato-prod-db-<TIMESTAMP>Z-<UUID>.manifest.json` (manifiesto con metadatos técnicos y hashes relativos)
- `hato-prod-db-<TIMESTAMP>Z-<UUID>.sha256` (checksum SHA256 del archivo .dump)
- `hato-prod-db-<TIMESTAMP>Z-<UUID>.complete` (marcador de verificación remota exitosa)

### Contenido del Manifiesto (`.manifest.json`)
```json
{
  "manifest_version": "1.0",
  "id": "hato-prod-db-20260916T070000Z-a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "environment": "production",
  "database": "hato_production",
  "created_at_utc": "2026-09-16T07:00:00Z",
  "postgres_version": "16",
  "release_sha": "git-commit-sha-here",
  "dump_file": "hato-prod-db-20260916T070000Z-a1b2c3d4-e5f6-7890-abcd-ef1234567890.dump",
  "dump_bytes": 1048576,
  "sha256": "abcdef...",
  "schemas": ["livestock", "production", "inventory", "people", "breeding", "tasks"],
  "verification_status": "verified",
  "key_version": "v1"
}
```

---

## 3. Comandos y Scripts Operativos

Todos los scripts admiten variables de entorno o archivo de configuración `/etc/hato-backup/backup.env` (permisos `600` de `hato-backup:hato-backup`).

### 3.1 Generación de Backup Local
```bash
# Ejecutar dump completo local y generar manifiesto con suma de verificación
./scripts/backup.sh
```
- Valida espacio en disco (mínimo 2x el último dump + 2 GiB de margen).
- Invoca helper root-owned `/usr/local/libexec/hato/backup-dump-root` que corre `pg_dump -Fc --no-owner --no-acl`.
- Escribe a `.partial` primero con permisos `600`.
- Verifica integridad del dump con `pg_restore --list`.
- Renombra a `.dump`, genera `.sha256` y `.manifest.json`.
- Si ocurre cualquier error, elimina el `.partial` y sale con código `1`.

### 3.2 Subida Cifrada a Google Drive
```bash
# Subir dump, manifiesto y checksum hacia el remote crypt de Google Drive
./scripts/backup-upload.sh /var/backups/hato-db/hato-prod-db-20260916T070000Z-<UUID>.dump
```
- Verifica cuota global y límite compartido de 300 GB antes de transferir.
- Serializa la admisión para evitar condiciones de carrera entre uploads concurrentes.
- Ejecuta `rclone copyto` hacia `hato-crypt:database/prod/daily/`.
- Comprueba integridad remota mediante `rclone cryptcheck` o verificación descifrada de hash SHA256.
- Publica el marcador `.complete` **solo** después de confirmar la integridad remota.
- Si el ID ya existe en remoto con un hash diferente, aborta inmediatamente sin sobrescribir.

### 3.3 Rotación y Retención
```bash
# Ejecutar verificación de retención en modo simulación (dry-run)
./scripts/backup-retention.sh --dry-run

# Aplicar poda real según política
./scripts/backup-retention.sh
```
- Poda archivos locales con antigüedad superior a 7 días.
- Poda archivos en Drive diarios superiores a 30 días, preservando la copia seleccionada mensual.
- Promueve/copia el respaldo mensual a `database/prod/monthly/` (retención 12 meses).
- **Salvaguarda absoluta:** Nunca elimina la última copia válida existente, sin importar su antigüedad.
- Filtra estrictamente por IDs y manifiestos propios del ERP; jamás toca archivos ajenos en la unidad.

### 3.4 Estado y Alertas
```bash
# Comprobar estado de los últimos respaldos y espacio
./scripts/backup-status.sh
```
- Reporta timestamp de último dump exitoso, RPO estimado y consumo de cuota en Drive.
- Emite señal de alerta si la última copia tiene más de 26 horas de antigüedad.

---

## 4. Automatización con Systemd y Monitorización Externa

### Unidades de Servicio y Temporizadores en VPS
Instalados en `/etc/systemd/system/`:
- `hato-backup.timer`: Se dispara diariamente a las `07:00 UTC` (02:00 Ecuador) con `Persistent=true`.
- `hato-backup.service`: Ejecuta el ciclo completo (backup local -> upload -> verificación -> rotación -> ping monitor).
  - Configurado con `User=hato-backup`, `Group=hato-backup`.
  - `TimeoutStartSec=1800` (evita procesos colgados).

### Monitorización Externa (Healthchecks en Home-Server + Email)
Conforme a ADR-0036:
1. **Deadman's Snitch / Heartbeat:**
   - URL secreta de ping configurada en `/etc/hato-backup/backup.env` (`HEALTHCHECKS_PING_URL`).
   - El script emite ping `/start` al comenzar, ping de éxito **únicamente** tras verificación remota completa, o ping `/fail` ante cualquier aborto.
2. **Plazos de Notificación:**
   - **Backup diario:** Notifica al email de la cuenta de backups si pasan **26 horas** sin ping de éxito (24h + 2h de gracia).
   - **Restauración mensual:** Notifica si transcurren **35 días** sin reporte de simulacro exitoso.
3. **Supervisión de VPS Caída:**
   - Dado que Healthchecks reside fuera de la VPS (en el home-server de staging), si la VPS de Oracle se apaga por completo o pierde conectividad, el temporizador de Healthchecks expira y alerta al propietario por correo electrónico.

---

## 5. Protocolo de Recuperación ante Desastres (Disaster Recovery Runbook)

Este protocolo describe la restauración completa tras la pérdida catastrófica de la VPS o corrupción total de datos.

### 5.1 Requisitos Previos y Entorno Limpio
1. Máquina o instancia aislada de recuperación con PostgreSQL 16 instalado.
2. **Prohibido:** No ejecutar este procedimiento apuntando al host ni al puerto de producción.
3. Claves de cifrado y configuración de rclone recuperadas del gestor de contraseñas seguro del propietario (copia offline fuera de Oracle y Google Drive).

### 5.2 Descarga y Verificación
```bash
# 1. Configurar temporalmente el remote rclone con las credenciales recuperadas
export RCLONE_CONFIG_HATO_DRIVE_TYPE=drive
# ... claves crypt y token ...

# 2. Descargar el backup seleccionado desde Drive
rclone copyto "hato-crypt:database/prod/daily/hato-prod-db-<TIMESTAMP>Z-<UUID>.dump" ./recuperado.dump
rclone copyto "hato-crypt:database/prod/daily/hato-prod-db-<TIMESTAMP>Z-<UUID>.manifest.json" ./recuperado.manifest.json
rclone copyto "hato-crypt:database/prod/daily/hato-prod-db-<TIMESTAMP>Z-<UUID>.sha256" ./recuperado.sha256

# 3. Comprobar checksum SHA-256 obligatorio
sha256sum -c recuperado.sha256
```

### 5.3 Ejecución del Restore Aislado
```bash
# Ejecutar restauración protegida
./scripts/restore.sh ./recuperado.dump hato_disaster_recovery_test
```
El script `scripts/restore.sh`:
- Comprueba que la base de datos destino no sea `hato_production` en un host activo de producción a menos que se use la bandera explícita de confirmación `--force-production-restore-disaster-only`.
- Valida la integridad del archivo y su checksum.
- Crea una base de datos limpia.
- Ejecuta `pg_restore --exit-on-error --no-owner --no-acl -d "$TARGET_DB" "$BACKUP_FILE"`.
- Reconstruye roles y esquemas (`livestock`, `production`, `inventory`, `people`, `breeding`, `tasks`).
- Valida conteos de registros esenciales, historial de eventos inmutables e integridad referencial.
- Emite código de salida no-cero ante cualquier tabla fallida o esquema ausente.

### 5.4 Registro y Reporte del Simulacro
- Documentar: ID del backup, tamaño, tiempo de descarga, tiempo de restauración, tiempo de verificación de API y RTO total obtenido.
- Comprobar que el RTO total observado no supere las **4 horas** de objetivo (Constitución Art. 2). Si se supera, se requiere ajuste de infraestructura o recalibración formal de la política.
- Guardar el reporte en el inventario privado de operaciones sin exponer datos personales ni contraseñas.

### 5.5 Procedimiento de Reautorización OAuth de Google Drive
Si el token de Google Drive caduca o se revoca:
1. Desde una máquina con entorno gráfico y navegador confiable bajo control del propietario:
   ```bash
   rclone authorize "drive" "<CLIENT_ID>" "<CLIENT_SECRET>"
   ```
2. Completar el inicio de sesión y consentimiento con la cuenta de backups.
3. Copiar el bloque JSON `{"access_token": ...}` recibido.
4. En el servidor, actualizar `/etc/hato-backup/rclone.conf` bajo `[hato-drive]` con el nuevo token.
5. Probar conectividad sin tocar datos de producción:
   ```bash
   rclone --config /etc/hato-backup/rclone.conf lsd hato-drive:
   ```

### 5.6 Comprobación Automatizada del Simulacro
El repositorio cuenta con una suite automatizada de simulacro de recuperación ante desastres:
```bash
tests/ops/backups/test-disaster-recovery-rehearsal.sh
```
Dicha suite genera un dump completo con esquemas modulares reales, descarga a un host limpio aislado, valida el checksum y el manifiesto, restaura mediante `scripts/restore.sh`, mide el RTO exacto, y valida la integridad de los 6 esquemas (`livestock`, `production`, `inventory`, `people`, `breeding`, `tasks`).

