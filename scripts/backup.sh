#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Automated PostgreSQL Backup Script (Art. 2 Compliance)
# Format: Custom (-Fc --no-owner --no-acl), Encrypted-ready, Fail-Closed
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Source optional environment file if running as system service
if [ -f "/etc/hato-backup/backup.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-backup/backup.env"
    set +a
fi

BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
ENVIRONMENT="${ENVIRONMENT:-production}"
POSTGRES_DB="${POSTGRES_DB:-hato_production}"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-hato-production-postgres}"
POSTGRES_USER="${POSTGRES_USER:-${PRODUCTION_POSTGRES_BACKUP_USER:-hato_backup}}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-${PRODUCTION_POSTGRES_BACKUP_PASSWORD:-}}"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
BACKUP_HELPER="${BACKUP_HELPER:-}"
LOCK_FILE="${BACKUP_LOCK_FILE:-/tmp/hato-backup.lock}"

# 1. Exclusive Execution Lock
mkdir -p "$(dirname "${LOCK_FILE}")"
exec 9>"${LOCK_FILE}"
if ! flock -n 9; then
    echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Otra instancia de backup se encuentra en ejecución." >&2
    exit 1
fi

mkdir -p "${BACKUP_DIR}"
chmod 700 "${BACKUP_DIR}" 2>/dev/null || true

# 2. Check Available Disk Space
# Requirement: at least 2x previous dump size + 2 GiB operational margin (default 2.5 GiB)
MARGIN_BYTES=$((2 * 1024 * 1024 * 1024))
LAST_DUMP="$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | awk '{print $2}' || true)"
if [ -n "${LAST_DUMP}" ] && [ -f "${LAST_DUMP}" ]; then
    LAST_SIZE=$(stat -c%s "${LAST_DUMP}" 2>/dev/null || stat -f%z "${LAST_DUMP}" 2>/dev/null || echo 0)
    REQUIRED_BYTES=$((2 * LAST_SIZE + MARGIN_BYTES))
else
    REQUIRED_BYTES=$((2500 * 1024 * 1024)) # 2.5 GiB
fi

AVAILABLE_BYTES=$(df -B1 "${BACKUP_DIR}" | awk 'NR==2 {print $4}')
if [ "${AVAILABLE_BYTES}" -lt "${REQUIRED_BYTES}" ]; then
    echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Espacio insuficiente en disco para backup:" >&2
    echo "  Requerido: ${REQUIRED_BYTES} bytes" >&2
    echo "  Disponible: ${AVAILABLE_BYTES} bytes" >&2
    exit 1
fi

# 3. Generate Execution Identifiers
TIMESTAMP=$(date -u +"%Y%m%dT%H%M%SZ")
UUID=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || python3 -c 'import uuid; print(uuid.uuid4())' 2>/dev/null || od -x /dev/urandom | head -1 | awk '{print $2$3"-"$4"-"$5"-"$6"-"$7$8$9}')
STEM="hato-${ENVIRONMENT}-db-${TIMESTAMP}-${UUID}"

PARTIAL_FILE="${BACKUP_DIR}/${STEM}.dump.partial"
FINAL_DUMP="${BACKUP_DIR}/${STEM}.dump"
SHA_FILE="${BACKUP_DIR}/${STEM}.sha256"
MANIFEST_FILE="${BACKUP_DIR}/${STEM}.manifest.json"

# Clean up partial on error
cleanup_partial() {
    if [ -f "${PARTIAL_FILE}" ]; then
        echo "[WARN] Limpiando archivo .partial tras fallo: ${PARTIAL_FILE}" >&2
        rm -f "${PARTIAL_FILE}"
    fi
}
trap cleanup_partial EXIT

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Iniciando backup PostgreSQL para base de datos '${POSTGRES_DB}' (ID: ${STEM})..."

# 4. Generate Dump with strict 600 permissions
(
    umask 077
    touch "${PARTIAL_FILE}"
)

DUMP_SUCCESS=0
if [ -n "${BACKUP_HELPER}" ]; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Ejecutando helper de dump: ${BACKUP_HELPER}..."
    if eval "${BACKUP_HELPER}" > "${PARTIAL_FILE}"; then
        DUMP_SUCCESS=1
    fi
elif [ -x "/usr/local/libexec/hato/backup-dump-root" ] && [ "$(id -u)" -ne 0 ]; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Ejecutando helper root-owned via sudo..."
    if sudo /usr/local/libexec/hato/backup-dump-root > "${PARTIAL_FILE}"; then
        DUMP_SUCCESS=1
    fi
elif command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Ejecutando pg_dump en contenedor '${POSTGRES_CONTAINER}'..."
    if docker exec "${POSTGRES_CONTAINER}" pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc --no-owner --no-acl > "${PARTIAL_FILE}"; then
        DUMP_SUCCESS=1
    fi
elif command -v pg_dump >/dev/null 2>&1; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Ejecutando pg_dump directo..."
    if PGPASSWORD="${POSTGRES_PASSWORD}" pg_dump -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc --no-owner --no-acl > "${PARTIAL_FILE}"; then
        DUMP_SUCCESS=1
    fi
else
    echo "[ERROR] No se encontró método disponible para ejecutar pg_dump (helper, contenedor docker o pg_dump local)." >&2
    exit 1
fi

if [ "${DUMP_SUCCESS}" -ne 1 ] || [ ! -s "${PARTIAL_FILE}" ]; then
    echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Falló la ejecución de pg_dump o el archivo generado está vacío." >&2
    exit 1
fi

# 5. Verify Dump Validity with pg_restore list if available
RESTORE_CHECK_PASSED=0
if command -v pg_restore >/dev/null 2>&1; then
    if pg_restore --list "${PARTIAL_FILE}" >/dev/null 2>&1; then
        RESTORE_CHECK_PASSED=1
    fi
elif command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
    if docker exec -i "${POSTGRES_CONTAINER}" pg_restore --list < "${PARTIAL_FILE}" >/dev/null 2>&1; then
        RESTORE_CHECK_PASSED=1
    fi
else
    # Fallback: check PostgreSQL custom format magic header (PGDMP / \x50\x47\x44\x4d\x50)
    MAGIC=$(head -c 5 "${PARTIAL_FILE}" || true)
    if [ "${MAGIC}" = "PGDMP" ]; then
        RESTORE_CHECK_PASSED=1
    fi
fi

if [ "${RESTORE_CHECK_PASSED}" -ne 1 ]; then
    echo "[ERROR] El dump generado no es un archivo válido en formato custom de PostgreSQL." >&2
    exit 1
fi

# 6. Atomic Rename to Final .dump
mv "${PARTIAL_FILE}" "${FINAL_DUMP}"
chmod 600 "${FINAL_DUMP}"
trap - EXIT # Disarm cleanup trap

# 7. Generate Checksum and Manifest
(cd "${BACKUP_DIR}" && sha256sum "$(basename "${FINAL_DUMP}")" > "${SHA_FILE}")
chmod 600 "${SHA_FILE}"

MANIFEST_SCRIPT="${SCRIPT_DIR}/backup-manifest.sh"
"${MANIFEST_SCRIPT}" generate "${FINAL_DUMP}" "${MANIFEST_FILE}" "${ENVIRONMENT}" "${POSTGRES_DB}"
chmod 600 "${MANIFEST_FILE}"

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Backup generado y verificado exitosamente:"
echo "  Dump:       ${FINAL_DUMP}"
echo "  Checksum:   ${SHA_FILE}"
echo "  Manifiesto: ${MANIFEST_FILE}"

# 8. Local Retention Cleanup (Safe Pruning)
# Never delete the newest valid backup regardless of age
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Aplicando política de retención local (${RETENTION_DAYS} días)..."
NEWEST_DUMP="$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | awk '{print $2}' || true)"

find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -mtime +"${RETENTION_DAYS}" | while read -r OLD_DUMP; do
    if [ "${OLD_DUMP}" != "${NEWEST_DUMP}" ]; then
        OLD_STEM="${OLD_DUMP%.dump}"
        echo "  [PODA LOCAL] Eliminando backup caducado: $(basename "${OLD_DUMP}")"
        rm -f "${OLD_DUMP}" "${OLD_STEM}.sha256" "${OLD_STEM}.manifest.json" "${OLD_STEM}.complete" 2>/dev/null || true
    else
        echo "  [PROTEGIDO] Conservando última copia válida a pesar de su antigüedad: $(basename "${OLD_DUMP}")"
    fi
done

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Proceso de backup local finalizado con éxito."
