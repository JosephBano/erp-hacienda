#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Backup Status and RPO/Quota Monitor
# Outputs status report, RPO metrics, and Drive storage threshold warnings.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source optional environment file
if [ -f "/etc/hato-backup/backup.env" ]; then
    # shellcheck disable=SC1091
    set -a
    . "/etc/hato-backup/backup.env"
    set +a
fi

OUTPUT_JSON=0
for arg in "$@"; do
    if [ "$arg" = "--json" ]; then
        OUTPUT_JSON=1
    fi
done

BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-backup/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
DRIVE_SHARED_LIMIT_BYTES="${DRIVE_SHARED_LIMIT_BYTES:-300000000000}"   # 300 GB decimal
DRIVE_WARN_THRESHOLD_BYTES="${DRIVE_WARN_THRESHOLD_BYTES:-210000000000}" # 210 GB (70%)
DRIVE_ALERT_THRESHOLD_BYTES="${DRIVE_ALERT_THRESHOLD_BYTES:-255000000000}" # 255 GB (85%)

# 1. Inspect Local Backups
LOCAL_COUNT=0
LATEST_DUMP=""
LATEST_TIMESTAMP=0
AGE_HOURS=999
STATUS="OK"

if [ -d "${BACKUP_DIR}" ]; then
    LOCAL_COUNT=$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f 2>/dev/null | wc -l)
    LATEST_DUMP="$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | awk '{print $2}' || true)"
    if [ -n "${LATEST_DUMP}" ] && [ -f "${LATEST_DUMP}" ]; then
        LATEST_TIMESTAMP=$(stat -c%Y "${LATEST_DUMP}" 2>/dev/null || stat -f%m "${LATEST_DUMP}" 2>/dev/null || echo 0)
        NOW=$(date +%s)
        AGE_SECONDS=$((NOW - LATEST_TIMESTAMP))
        AGE_HOURS=$((AGE_SECONDS / 3600))
    fi
fi

# RPO Evaluation (Target 24h, Alert at 26h)
if [ "${LOCAL_COUNT}" -eq 0 ]; then
    STATUS="CRITICAL_NO_BACKUP"
elif [ "${AGE_HOURS}" -ge 26 ]; then
    STATUS="CRITICAL_RPO_EXCEEDED"
elif [ "${AGE_HOURS}" -ge 24 ]; then
    STATUS="WARNING_RPO_STALE"
fi

# 2. Remote Drive Storage Consumption
REMOTE_BYTES=0
if [ -n "${MOCK_REMOTE_BYTES:-}" ]; then
    REMOTE_BYTES="${MOCK_REMOTE_BYTES}"
elif [ -f "${RCLONE_CONFIG}" ] && command -v "${RCLONE_CMD}" >/dev/null 2>&1; then
    SIZE_OUT=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" size "${RCLONE_REMOTE}:" 2>/dev/null || true)
    if [ -n "${SIZE_OUT}" ]; then
        REMOTE_BYTES=$(echo "${SIZE_OUT}" | grep -i "Total size:" | grep -o '([0-9]\+ Byte' | tr -dc '0-9' || echo 0)
    fi
fi

STORAGE_STATUS="OK"
if [ "${REMOTE_BYTES}" -ge "${DRIVE_SHARED_LIMIT_BYTES}" ]; then
    STORAGE_STATUS="CRITICAL_LIMIT_EXCEEDED"
    STATUS="CRITICAL_STORAGE"
elif [ "${REMOTE_BYTES}" -ge "${DRIVE_ALERT_THRESHOLD_BYTES}" ]; then
    STORAGE_STATUS="ALERT_THRESHOLD_85"
    if [ "${STATUS}" = "OK" ]; then STATUS="WARNING_STORAGE"; fi
elif [ "${REMOTE_BYTES}" -ge "${DRIVE_WARN_THRESHOLD_BYTES}" ]; then
    STORAGE_STATUS="WARN_THRESHOLD_70"
    if [ "${STATUS}" = "OK" ]; then STATUS="WARNING_STORAGE"; fi
fi

if [ "${OUTPUT_JSON}" -eq 1 ]; then
    python3 -c "
import json, sys
data = {
    'status': sys.argv[1],
    'storage_status': sys.argv[2],
    'local_backups_count': int(sys.argv[3]),
    'latest_backup_file': sys.argv[4],
    'latest_backup_age_hours': int(sys.argv[5]),
    'remote_used_bytes': int(sys.argv[6]),
    'drive_shared_limit_bytes': int(sys.argv[7]),
    'evaluated_at_utc': sys.argv[8]
}
print(json.dumps(data, indent=2))
" "${STATUS}" "${STORAGE_STATUS}" "${LOCAL_COUNT}" "${LATEST_DUMP}" "${AGE_HOURS}" "${REMOTE_BYTES}" "${DRIVE_SHARED_LIMIT_BYTES}" "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
else
    echo "=================================================================="
    echo " ERP HATO - Estado de Backups y Almacenamiento Offsite"
    echo "=================================================================="
    echo "  Estado General:          ${STATUS}"
    echo "  Copias Locales:          ${LOCAL_COUNT}"
    echo "  Último Backup:           ${LATEST_DUMP:-Ninguno}"
    echo "  Antigüedad del Backup:   ${AGE_HOURS} horas (RPO objetivo: 24h, alerta: 26h)"
    echo "  Uso en Google Drive:     ${REMOTE_BYTES} / ${DRIVE_SHARED_LIMIT_BYTES} bytes"
    echo "  Estado de Almacenamiento: ${STORAGE_STATUS}"
    echo "=================================================================="
fi

if [[ "${STATUS}" == CRITICAL* ]]; then
    exit 1
fi
exit 0
