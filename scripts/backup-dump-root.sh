#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Root-Owned PostgreSQL Dump Helper
# Installed at: /usr/local/libexec/hato/backup-dump-root (root:root, 755)
# Invoked via sudo by 'hato-backup' without granting Docker socket or general sudo.
# ==============================================================================

# Source production environment if present (contains role names and secrets)
if [ -f "/etc/hato-production/production.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-production/production.env"
    set +a
elif [ -f "/etc/hato-backup/backup.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-backup/backup.env"
    set +a
fi

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-hato-production-postgres}"
POSTGRES_DB="${POSTGRES_DB:-hato_production}"
BACKUP_USER="${PRODUCTION_POSTGRES_BACKUP_USER:-${POSTGRES_USER:-hato_backup}}"
BACKUP_PASSWORD="${PRODUCTION_POSTGRES_BACKUP_PASSWORD:-${POSTGRES_PASSWORD:-}}"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"

# Stream custom format dump directly to stdout
if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
    exec docker exec -i "${POSTGRES_CONTAINER}" pg_dump -U "${BACKUP_USER}" -d "${POSTGRES_DB}" -Fc --no-owner --no-acl
elif command -v pg_dump >/dev/null 2>&1; then
    export PGPASSWORD="${BACKUP_PASSWORD}"
    exec pg_dump -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${BACKUP_USER}" -d "${POSTGRES_DB}" -Fc --no-owner --no-acl
else
    echo "[ERROR] [backup-dump-root] No se puede ejecutar pg_dump: docker o pg_dump no disponibles." >&2
    exit 1
fi
