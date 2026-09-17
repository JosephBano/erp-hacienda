#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Daily Backup Pipeline Orchestrator
# Invoked by systemd service hato-backup.service.
# Executes: local backup -> encrypted upload -> retention pruning -> monitor ping
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source optional environment file
if [ -f "/etc/hato-backup/backup.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-backup/backup.env"
    set +a
fi

HEALTHCHECKS_PING_URL="${HEALTHCHECKS_PING_URL:-}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"

# Notify Healthchecks of job start
if [ -n "${HEALTHCHECKS_PING_URL}" ]; then
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_PING_URL}/start" >/dev/null 2>&1 || true
fi

notify_on_exit() {
    local EXIT_CODE=$?
    if [ "${EXIT_CODE}" -ne 0 ]; then
        echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] El pipeline de backup falló (código ${EXIT_CODE})." >&2
        if [ -n "${HEALTHCHECKS_PING_URL:-}" ]; then
            echo "[INFO] Enviando señal /fail a Healthchecks..." >&2
            curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_PING_URL}/fail" >/dev/null 2>&1 || true
        fi
    fi
}
trap notify_on_exit EXIT

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] === PASO 1: Generando backup local PostgreSQL ==="
"${SCRIPT_DIR}/backup.sh"

LATEST_DUMP="$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | awk '{print $2}' || true)"
if [ -z "${LATEST_DUMP}" ] || [ ! -f "${LATEST_DUMP}" ]; then
    echo "[ERROR] No se encontró el archivo de dump generado en ${BACKUP_DIR}." >&2
    exit 1
fi

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] === PASO 2: Subiendo backup cifrado a Google Drive ==="
"${SCRIPT_DIR}/backup-upload.sh" "${LATEST_DUMP}"

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] === PASO 3: Ejecutando retención y poda segura ==="
"${SCRIPT_DIR}/backup-retention.sh"

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] === PIPELINE DE BACKUP COMPLETADO EXITOSAMENTE ==="
