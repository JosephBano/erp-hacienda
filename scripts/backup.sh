#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Automated PostgreSQL Backup Script (Art. 2 Compliance)
# ==============================================================================

BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-hato-postgres}"
POSTGRES_DB="${POSTGRES_DB:-hato_prod}"
POSTGRES_USER="${POSTGRES_USER:-hato}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

TIMESTAMP=$(date -u +"%Y%m%d_%H%M%SZ")
BACKUP_FILE="${BACKUP_DIR}/hato_backup_${TIMESTAMP}.sql.gz"
CHECKSUM_FILE="${BACKUP_FILE}.sha256"

mkdir -p "${BACKUP_DIR}"

echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Starting PostgreSQL backup for database: ${POSTGRES_DB}..."

# Execute pg_dump and compress
if docker exec "${POSTGRES_CONTAINER}" pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" --clean --if-exists --create | gzip -9 > "${BACKUP_FILE}"; then
    echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Backup generated successfully: ${BACKUP_FILE}"
else
    echo "[ERROR] Backup execution failed!" >&2
    exit 1
fi

# Calculate SHA256 Checksum
sha256sum "${BACKUP_FILE}" > "${CHECKSUM_FILE}"
echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Checksum created: $(cat "${CHECKSUM_FILE}")"

# Retention Cleanup (Delete backups older than RETENTION_DAYS)
echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Cleaning up backups older than ${RETENTION_DAYS} days..."
find "${BACKUP_DIR}" -name "hato_backup_*.sql.gz*" -type f -mtime +"${RETENTION_DAYS}" -delete

echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Backup process completed successfully."
