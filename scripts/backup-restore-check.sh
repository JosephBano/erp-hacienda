#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Monthly Isolated Restore Verification Script
# Runs only on home-server. It downloads a completed backup from Google Drive,
# creates a dedicated temporary PostgreSQL container, restores to it, validates
# schemas and row integrity, and finally destroys the isolated container.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source the home-server-only configuration. It intentionally differs from
# /etc/hato-backup/backup.env so this host never receives Oracle DB credentials.
RESTORE_ENV_FILE="${HATO_RESTORE_ENV_FILE:-/etc/hato-restore/restore.env}"
if [ -f "${RESTORE_ENV_FILE}" ]; then
    # shellcheck disable=SC1091
    set -a
    . "${RESTORE_ENV_FILE}"
    set +a
fi

HEALTHCHECKS_RESTORE_PING_URL="${HEALTHCHECKS_RESTORE_PING_URL:-}"
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-restore/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
REMOTE_NAMESPACE="${REMOTE_NAMESPACE:-database/prod/daily}"
RESTORE_COMPOSE_FILE="${RESTORE_COMPOSE_FILE:-/opt/hato-restore/hato-restore-check.compose.yml}"
RESTORE_POSTGRES_CONTAINER="${RESTORE_POSTGRES_CONTAINER:-}"
RESTORE_POSTGRES_USER="${RESTORE_POSTGRES_USER:-hato_restore}"
RESTORE_POSTGRES_PASSWORD="${RESTORE_POSTGRES_PASSWORD:-}"
TEMP_RESTORE_DIR="$(mktemp -d /tmp/hato-monthly-restore.XXXXXX)"
TEST_DB="hato_monthly_check_$(date +%s)"

cleanup() {
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Limpiando recursos de restauración temporal..."
    if [ -f "${RESTORE_COMPOSE_FILE}" ] && command -v docker >/dev/null 2>&1; then
        docker compose --env-file "${RESTORE_ENV_FILE}" -f "${RESTORE_COMPOSE_FILE}" down --volumes --remove-orphans >/dev/null 2>&1 || true
    fi
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

if [ "${RESTORE_POSTGRES_CONTAINER}" != "hato-restore-check-postgres" ]; then
    echo "[ERROR] El contenedor aislado debe ser exactamente 'hato-restore-check-postgres'; se rechazó '${RESTORE_POSTGRES_CONTAINER:-vacío}'." >&2
    exit 1
fi

if [ ! -f "${RESTORE_ENV_FILE}" ]; then
    echo "[ERROR] No existe la configuración exclusiva del home-server: ${RESTORE_ENV_FILE}" >&2
    exit 1
fi

if [ ! -f "${RESTORE_COMPOSE_FILE}" ]; then
    echo "[ERROR] No existe el compose del contenedor aislado: ${RESTORE_COMPOSE_FILE}" >&2
    exit 1
fi

if [ ! -f "${RCLONE_CONFIG}" ] || ! command -v "${RCLONE_CMD}" >/dev/null 2>&1; then
    echo "[ERROR] rclone o su configuración exclusiva del home-server no están disponibles." >&2
    exit 1
fi

if [ -z "${RESTORE_POSTGRES_PASSWORD}" ]; then
    echo "[ERROR] Falta RESTORE_POSTGRES_PASSWORD para el PostgreSQL aislado." >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "[ERROR] Docker es necesario para crear el PostgreSQL aislado." >&2
    exit 1
fi

if [ -n "${HEALTHCHECKS_RESTORE_PING_URL}" ]; then
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_RESTORE_PING_URL}/start" >/dev/null 2>&1 || true
fi

# 1. Locate the latest completed backup in remote. A dump alone is not valid;
# the marker is published only after remote verification by the daily pipeline.
REMOTE_PATH="${RCLONE_REMOTE}:${REMOTE_NAMESPACE}"
echo "Buscando último backup completo en ${REMOTE_PATH}..."

LATEST_COMPLETE_NAME=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" lsf --files-only "${REMOTE_PATH}" 2>/dev/null | grep -E '^hato-prod-db-.*\.complete$' | sort | tail -n 1 || true)

if [ -z "${LATEST_COMPLETE_NAME}" ]; then
    echo "[ERROR] No se encontró un backup remoto marcado como completo para la prueba de restauración." >&2
    exit 1
fi

STEM="${LATEST_COMPLETE_NAME%.complete}"
LATEST_DUMP_NAME="${STEM}.dump"
echo "Descargando ${STEM} desde Drive..."
for suffix in dump sha256 manifest.json complete; do
    "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" copyto \
        "${REMOTE_PATH}/${STEM}.${suffix}" "${TEMP_RESTORE_DIR}/${STEM}.${suffix}"
done
DOWNLOADED_DUMP="${TEMP_RESTORE_DIR}/${LATEST_DUMP_NAME}"

# 2. Build the dedicated PostgreSQL first. The compose has no ports and an
# internal network, so this restore cannot reach Oracle's production database.
docker compose --env-file "${RESTORE_ENV_FILE}" -f "${RESTORE_COMPOSE_FILE}" up -d --wait

# 3. Execute restore.sh against the dedicated test DB.
echo "Restaurando en base aislada: ${TEST_DB}..."
POSTGRES_CONTAINER="${RESTORE_POSTGRES_CONTAINER}" \
POSTGRES_USER="${RESTORE_POSTGRES_USER}" \
POSTGRES_PASSWORD="${RESTORE_POSTGRES_PASSWORD}" \
"${SCRIPT_DIR}/restore.sh" "${DOWNLOADED_DUMP}" "${TEST_DB}"

trap - ERR # Success

if [ -n "${HEALTHCHECKS_RESTORE_PING_URL}" ]; then
    echo "Enviando ping de éxito a Healthchecks..."
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_RESTORE_PING_URL}" >/dev/null 2>&1 || true
fi

echo "[EXITO] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificación mensual de restauración completada exitosamente."
