# 💾 Estrategia y Protocolo de Backups Automáticos (Art. 2)

> **Constitución Art. 2**: *"Los backups deben estar automatizados y probarse periódicamente con una restauración real; un backup no probado es un deseo, no una copia de seguridad."*

---

## 📋 Arquitectura de Respaldos

El ERP HATO utiliza PostgreSQL 16 con esquemas modulares desacoplados (`livestock`, `production`, `inventory`, `people`). Toda copia de seguridad debe cumplir con:

1. **Inmutabilidad y Compresión**: Exportación completa mediante `pg_dump --clean --if-exists --create` comprimido en formato `.sql.gz`.
2. **Verificación de Integridad**: Generación automática de archivo checksum `SHA-256`.
3. **Política de Retención**: Mantenimiento automático de los últimos 30 días de backups diarios.

---

## 🛠️ Ejecución Manual de Backup

Para generar una copia de seguridad en cualquier momento:

```bash
./scripts/backup.sh
```

Los archivos se guardarán en `/var/backups/hato-db/` con el patrón `hato_backup_YYYYMMDD_HHMMSSZ.sql.gz`.

---

## ⏰ Automatización vía Cron Job

Para configurar el respaldo automático diario a las 02:00 AM UTC, agregar la siguiente entrada a `crontab -e`:

```cron
0 2 * * * /home/joeman/Documents/proyects/erp-hacienda/scripts/backup.sh >> /var/log/hato_backup.log 2>&1
```

---

## 🧪 Protocolo Obligatorio de Prueba de Restauración

Para validar que la copia de seguridad no está corrupta y se puede restaurar en una base de datos limpia de pruebas:

```bash
./scripts/restore.sh /var/backups/hato-db/hato_backup_20260801_020000Z.sql.gz hato_restore_test
```

El script validará automáticamente:
1. Integridad de la suma de verificación SHA-256.
2. Creación limpia de base de datos.
3. Importación de esquemas y conteo de datos esenciales en `livestock.animals`.
