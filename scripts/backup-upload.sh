#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Encrypted Backup Upload to Google Drive
# Enforces: 300 GB decimal limit, 210/255 GB thresholds, remote verification,
# complete marker publication, and serial admission control.
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

DUMP_FILE="${1:-}"
if [ -z "${DUMP_FILE}" ] || [ ! -f "${DUMP_FILE}" ]; then
    echo "[ERROR] Uso: $0 <archivo_dump.dump>" >&2
    exit 1
fi

if [[ "${DUMP_FILE}" == *.partial ]]; then
    echo "[ERROR] No se permite subir un archivo parcial (.partial)." >&2
    exit 1
fi

# Configuration & Defaults
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-backup/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
REMOTE_NAMESPACE="${REMOTE_NAMESPACE:-database/prod/daily}"
DRIVE_SHARED_LIMIT_BYTES="${DRIVE_SHARED_LIMIT_BYTES:-300000000000}"   # 300 GB decimal
DRIVE_WARN_THRESHOLD_BYTES="${DRIVE_WARN_THRESHOLD_BYTES:-210000000000}" # 210 GB (70%)
DRIVE_ALERT_THRESHOLD_BYTES="${DRIVE_ALERT_THRESHOLD_BYTES:-255000000000}" # 255 GB (85%)
HEALTHCHECKS_PING_URL="${HEALTHCHECKS_PING_URL:-}"
UPLOAD_LOCK_FILE="${UPLOAD_LOCK_FILE:-/tmp/hato-drive-upload.lock}"

DUMP_DIR="$(cd "$(dirname "${DUMP_FILE}")" && pwd)"
DUMP_BASENAME="$(basename "${DUMP_FILE}")"
STEM="${DUMP_BASENAME%.dump}"

SHA_FILE="${DUMP_DIR}/${STEM}.sha256"
MANIFEST_FILE="${DUMP_DIR}/${STEM}.manifest.json"
COMPLETE_FILE="${DUMP_DIR}/${STEM}.complete"

if [ ! -f "${SHA_FILE}" ]; then
    echo "[ERROR] Checksum '${SHA_FILE}' no existe. La integridad local es obligatoria antes de subir." >&2
    exit 1
fi

# 1. Local Checksum Pre-validation
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Validando integridad local antes de subir..."
if ! (cd "${DUMP_DIR}" && sha256sum -c "$(basename "${SHA_FILE}")" >/dev/null 2>&1); then
    echo "[ERROR] El archivo local '${DUMP_FILE}' no coincide con su checksum SHA256." >&2
    exit 1
fi

# 2. Serialized Admission Control (Mutex across all drive uploads including APKs/monitoring)
mkdir -p "$(dirname "${UPLOAD_LOCK_FILE}")"
exec 8>"${UPLOAD_LOCK_FILE}"
if ! flock -w 120 8; then
    echo "[ERROR] No se pudo obtener el bloqueo de subida a Drive tras 120s. Otra subida concurrente está activa." >&2
    exit 1
fi

# 3. Check Remote Storage Consumption and Drive Shared Limit
DUMP_BYTES=$(stat -c%s "${DUMP_FILE}" 2>/dev/null || stat -f%z "${DUMP_FILE}" 2>/dev/null || echo 0)
MANIFEST_BYTES=0
[ -f "${MANIFEST_FILE}" ] && MANIFEST_BYTES=$(stat -c%s "${MANIFEST_FILE}" 2>/dev/null || echo 0)
UPLOAD_TOTAL_BYTES=$((DUMP_BYTES + MANIFEST_BYTES + 1024))

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Comprobando cuota y límite de almacenamiento en Google Drive..."
CURRENT_REMOTE_BYTES=0

if [ -n "${MOCK_REMOTE_BYTES:-}" ]; then
    CURRENT_REMOTE_BYTES="${MOCK_REMOTE_BYTES}"
elif [ -f "${RCLONE_CONFIG}" ] && command -v "${RCLONE_CMD}" >/dev/null 2>&1; then
    SIZE_OUTPUT=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" size "${RCLONE_REMOTE}:" 2>/dev/null || true)
    if [ -n "${SIZE_OUTPUT}" ]; then
        CURRENT_REMOTE_BYTES=$(echo "${SIZE_OUTPUT}" | grep -i "Total size:" | grep -o '([0-9]\+ Byte' | tr -dc '0-9' || echo 0)
    fi
fi

PROJECTED_BYTES=$((CURRENT_REMOTE_BYTES + UPLOAD_TOTAL_BYTES))

echo "  Uso actual remoto:      ${CURRENT_REMOTE_BYTES} bytes"
echo "  Tamaño a subir:         ${UPLOAD_TOTAL_BYTES} bytes"
echo "  Proyección post-subida: ${PROJECTED_BYTES} bytes (Límite: ${DRIVE_SHARED_LIMIT_BYTES} bytes)"

# Enforcement of 300 GB Decimal Shared Limit
if [ "${PROJECTED_BYTES}" -gt "${DRIVE_SHARED_LIMIT_BYTES}" ]; then
    echo "[ERROR] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] La subida excede el límite operativo de 300 GB decimales." >&2
    echo "  Límite: ${DRIVE_SHARED_LIMIT_BYTES} B, Proyectado: ${PROJECTED_BYTES} B." >&2
    echo "  Conservando copia local sin alterar retención remota." >&2
    exit 1
fi

# Threshold Warnings
if [ "${PROJECTED_BYTES}" -ge "${DRIVE_ALERT_THRESHOLD_BYTES}" ]; then
    echo "[ALERTA CRITICA] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] El almacenamiento en Drive supera el umbral urgente de 255 GB (85 %)." >&2
elif [ "${PROJECTED_BYTES}" -ge "${DRIVE_WARN_THRESHOLD_BYTES}" ]; then
    echo "[ADVERTENCIA] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] El almacenamiento en Drive supera el umbral preventivo de 210 GB (70 %)." >&2
fi

# 4. Conflict Check: Refuse to overwrite if same ID exists with different hash
if [[ "${RCLONE_REMOTE}" == *:* ]]; then
    DEST_PREFIX="${RCLONE_REMOTE}/${REMOTE_NAMESPACE}"
elif [[ "${RCLONE_REMOTE}" == /* ]]; then
    DEST_PREFIX="${RCLONE_REMOTE}/${REMOTE_NAMESPACE}"
else
    DEST_PREFIX="${RCLONE_REMOTE}:${REMOTE_NAMESPACE}"
fi
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificando colisiones de identificador en destino remoto..."

# Helper to run rclone commands
rclone_exec() {
    if [ -f "${RCLONE_CONFIG}" ]; then
        "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" "$@"
    else
        "${RCLONE_CMD}" "$@"
    fi
}

# Check if file exists in remote
EXISTING_CHECK=$(rclone_exec lsf "${DEST_PREFIX}/${DUMP_BASENAME}" 2>/dev/null || true)
if [ -n "${EXISTING_CHECK}" ]; then
    echo "[WARN] El archivo '${DUMP_BASENAME}' ya existe en remoto. Comprobando hash..."
    TEMP_VERIFY_DIR="$(mktemp -d /tmp/hato-verify-conflict.XXXXXX)"
    rclone_exec copyto "${DEST_PREFIX}/${DUMP_BASENAME}" "${TEMP_VERIFY_DIR}/${DUMP_BASENAME}" || true
    REMOTE_HASH=""
    if [ -f "${TEMP_VERIFY_DIR}/${DUMP_BASENAME}" ]; then
        REMOTE_HASH=$(sha256sum "${TEMP_VERIFY_DIR}/${DUMP_BASENAME}" | awk '{print $1}')
    fi
    rm -rf "${TEMP_VERIFY_DIR}"

    LOCAL_HASH=$(awk '{print $1}' "${SHA_FILE}")
    if [ "${REMOTE_HASH}" != "${LOCAL_HASH}" ]; then
        echo "[ERROR] [CONFLICTO CRITICO] El archivo remoto '${DUMP_BASENAME}' existe con un hash diferente!" >&2
        echo "  Local:  ${LOCAL_HASH}" >&2
        echo "  Remoto: ${REMOTE_HASH}" >&2
        echo "  Por seguridad y fail-closed, NUNCA se sobreescribe un backup con conflicto de ID." >&2
        exit 1
    else
        echo "[OK] El archivo remoto ya existe con el mismo hash idéntico. Continuando con marcador complete..."
    fi
fi

# 5. Upload files with retry and exponential backoff
upload_with_retry() {
    local SRC="$1"
    local DEST="$2"
    local MAX_RETRIES=3
    local RETRY=0
    local DELAY=2

    while [ "${RETRY}" -lt "${MAX_RETRIES}" ]; do
        if rclone_exec copyto "${SRC}" "${DEST}"; then
            return 0
        fi
        RETRY=$((RETRY + 1))
        echo "[WARN] Intento ${RETRY}/${MAX_RETRIES} falló para transferir $(basename "${SRC}"). Reintentando en ${DELAY}s..." >&2
        sleep "${DELAY}"
        DELAY=$((DELAY * 2))
    done
    return 1
}

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Subiendo dump y archivos compañeros a ${DEST_PREFIX}..."
if [ -f "${MANIFEST_FILE}" ]; then
    upload_with_retry "${MANIFEST_FILE}" "${DEST_PREFIX}/${STEM}.manifest.json" || {
        echo "[ERROR] Falló la subida del manifiesto a remoto." >&2
        exit 1
    }
fi

upload_with_retry "${SHA_FILE}" "${DEST_PREFIX}/${STEM}.sha256" || {
    echo "[ERROR] Falló la subida del checksum a remoto." >&2
    exit 1
}

upload_with_retry "${DUMP_FILE}" "${DEST_PREFIX}/${DUMP_BASENAME}" || {
    echo "[ERROR] Falló la subida del dump binario a remoto." >&2
    exit 1
}

# 6. Remote Verification (Cryptcheck or downloaded hash comparison)
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificando integridad remota tras la subida..."
VERIFIED=0
if rclone_exec check "${DUMP_DIR}" "${DEST_PREFIX}" --include "${DUMP_BASENAME}" >/dev/null 2>&1; then
    VERIFIED=1
elif rclone_exec cryptcheck "${DUMP_DIR}" "${DEST_PREFIX}" --include "${DUMP_BASENAME}" >/dev/null 2>&1; then
    VERIFIED=1
else
    # Direct download check
    TEMP_CHECK_DIR="$(mktemp -d /tmp/hato-remote-check.XXXXXX)"
    if rclone_exec copyto "${DEST_PREFIX}/${DUMP_BASENAME}" "${TEMP_CHECK_DIR}/${DUMP_BASENAME}"; then
        CHECK_HASH=$(sha256sum "${TEMP_CHECK_DIR}/${DUMP_BASENAME}" | awk '{print $1}')
        LOCAL_HASH=$(awk '{print $1}' "${SHA_FILE}")
        if [ "${CHECK_HASH}" = "${LOCAL_HASH}" ]; then
            VERIFIED=1
        fi
    fi
    rm -rf "${TEMP_CHECK_DIR}"
fi

if [ "${VERIFIED}" -ne 1 ]; then
    echo "[ERROR] Verificación de integridad remota fallida. NO se publicará marcador .complete." >&2
    exit 1
fi
echo "[OK] Integridad remota verificada exitosamente."

# 7. Publish .complete marker ONLY after full verification
echo "verified_at_utc=$(date -u +"%Y-%m-%dT%H:%M:%SZ")" > "${COMPLETE_FILE}"
echo "stem=${STEM}" >> "${COMPLETE_FILE}"
echo "sha256=$(awk '{print $1}' "${SHA_FILE}")" >> "${COMPLETE_FILE}"

upload_with_retry "${COMPLETE_FILE}" "${DEST_PREFIX}/${STEM}.complete" || {
    echo "[ERROR] Falló la publicación remota del marcador .complete." >&2
    exit 1
}

echo "[EXITO] [$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Backup '${STEM}' subido y verificado en Google Drive."

# 8. External Monitor Ping (Healthchecks deadman's snitch)
if [ -n "${HEALTHCHECKS_PING_URL}" ]; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Enviando ping de éxito a Healthchecks..."
    curl -fsS -m 10 --retry 3 "${HEALTHCHECKS_PING_URL}" >/dev/null 2>&1 || {
        echo "[WARN] No se pudo enviar el ping a Healthchecks, pero el backup remoto se completó." >&2
    }
fi
