#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Monthly Isolated Restore Verification Script
# Downloads the latest backup from Google Drive, restores to an isolated test DB,
# validates domain schemas and row integrity, and pings Healthchecks.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Source optional environment file
if [ -f "/etc/hato-backup/backup.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-backup/backup.env"
    set +a
fi

HEALTHCHECKS_RESTORE_PING_URL="${HEALTHCHECKS_RESTORE_PING_URL:-}"
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-backup/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
REMOTE_NAMESPACE="${REMOTE_NAMESPACE:-database/prod/daily}"
TEMP_RESTORE_DIR="$(mktemp -d /tmp/hato-monthly-restore.XXXXXX)"
TEST_DB="hato_monthly_check_$(date +%s)"

cleanup() {
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Limpiando recursos de restauración temporal..."
    rm -rf "${TEMP_RESTORE_DIR}"
}
trap cleanup EXIT

fail_notify() {
    echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] La verificación mensual de restauración falló." >&2
    if [ -n "${HEALTHCHECKS_RESTORE_PING_URL}" ]; then
        curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_RESTORE_PING_URL}/fail" >/dev/null 2>&1 || true
    fi
}
trap fail_notify ERR

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] === Iniciando verificación mensual de restauración aislada ==="

if [ -n "${HEALTHCHECKS_RESTORE_PING_URL}" ]; then
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_RESTORE_PING_URL}/start" >/dev/null 2>&1 || true
fi

# 1. Locate latest completed backup in remote
REMOTE_PATH="${RCLONE_REMOTE}:${REMOTE_NAMESPACE}"
echo "Buscando último backup en ${REMOTE_PATH}..."

LATEST_DUMP_NAME=""
if [ -f "${RCLONE_CONFIG}" ] && command -v "${RCLONE_CMD}" >/dev/null 2>&1; then
    LATEST_DUMP_NAME=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" lsf "${REMOTE_PATH}" 2>/dev/null | grep -E '^hato-.*-db-.*\.dump$' | sort | tail -n 1 || true)
fi

if [ -z "${LATEST_DUMP_NAME}" ]; then
    # Fallback to local backup dir if remote not configured
    BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
    LOCAL_LATEST=$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f 2>/dev/null | sort | tail -n 1 || true)
    if [ -n "${LOCAL_LATEST}" ]; then
        cp "${LOCAL_LATEST%.dump}".* "${TEMP_RESTORE_DIR}/"
        DOWNLOADED_DUMP="${TEMP_RESTORE_DIR}/$(basename "${LOCAL_LATEST}")"
    else
        echo "[ERROR] No se encontró ningún backup para la prueba de restauración." >&2
        exit 1
    fi
else
    STEM="${LATEST_DUMP_NAME%.dump}"
    echo "Descargando ${STEM} desde Drive..."
    "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" copyto "${REMOTE_PATH}/${LATEST_DUMP_NAME}" "${TEMP_RESTORE_DIR}/${LATEST_DUMP_NAME}"
    "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" copyto "${REMOTE_PATH}/${STEM}.sha256" "${TEMP_RESTORE_DIR}/${STEM}.sha256" || true
    "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" copyto "${REMOTE_PATH}/${STEM}.manifest.json" "${TEMP_RESTORE_DIR}/${STEM}.manifest.json" || true
    DOWNLOADED_DUMP="${TEMP_RESTORE_DIR}/${LATEST_DUMP_NAME}"
fi

# 2. Execute restore.sh against isolated test DB
echo "Restaurando en base aislada: ${TEST_DB}..."
"${SCRIPT_DIR}/restore.sh" "${DOWNLOADED_DUMP}" "${TEST_DB}"

trap - ERR # Success

if [ -n "${HEALTHCHECKS_RESTORE_PING_URL}" ]; then
    echo "Enviando ping de éxito a Healthchecks..."
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_RESTORE_PING_URL}" >/dev/null 2>&1 || true
fi

echo "[EXITO] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificación mensual de restauración completada exitosamente."
