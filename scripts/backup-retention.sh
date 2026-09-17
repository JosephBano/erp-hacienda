#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Safe Backup Retention & Rotation
# Policies: 7 days local, 30 days daily remote, 12 months monthly remote.
# Protects last valid copy, operates ONLY on owned stems, supports --dry-run.
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

DRY_RUN=0
SCOPE="all" # all, local, remote

while [ "$#" -gt 0 ]; do
    case "$1" in
        --dry-run)
            DRY_RUN=1
            shift
            ;;
        --local-only)
            SCOPE="local"
            shift
            ;;
        --remote-only)
            SCOPE="remote"
            shift
            ;;
        *)
            echo "Opción desconocida: $1" >&2
            exit 1
            ;;
    esac
done

BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-backup/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
RETENTION_LOCAL_DAYS="${RETENTION_LOCAL_DAYS:-7}"
RETENTION_DAILY_DAYS="${RETENTION_REMOTE_DAILY_DAYS:-30}"
RETENTION_MONTHLY_DAYS=$(( ${RETENTION_REMOTE_MONTHLY_MONTHS:-12} * 30 ))

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Iniciando proceso de retención (Modo Dry-Run: ${DRY_RUN}, Alcance: ${SCOPE})..."

# ==============================================================================
# 1. Local Retention (7 Days)
# ==============================================================================
if [ "${SCOPE}" = "all" ] || [ "${SCOPE}" = "local" ]; then
    if [ -d "${BACKUP_DIR}" ]; then
        echo "[LOCAL] Evaluando copias locales en ${BACKUP_DIR}..."
        # Find newest valid dump to protect it
        NEWEST_LOCAL="$(find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | awk '{print $2}' || true)"

        find "${BACKUP_DIR}" -name "hato-*-db-*.dump" -type f -mtime +"${RETENTION_LOCAL_DAYS}" 2>/dev/null | while read -r EXPIRED_LOCAL; do
            if [ "${EXPIRED_LOCAL}" = "${NEWEST_LOCAL}" ]; then
                echo "  [LOCAL-PROTEGIDO] Conservando última copia válida local: $(basename "${EXPIRED_LOCAL}")"
                continue
            fi
            STEM="${EXPIRED_LOCAL%.dump}"
            if [ "${DRY_RUN}" -eq 1 ]; then
                echo "  [DRY-RUN] [LOCAL-PODA] Se eliminaría copia local caducada: $(basename "${EXPIRED_LOCAL}")"
            else
                echo "  [LOCAL-PODA] Eliminando copia local caducada: $(basename "${EXPIRED_LOCAL}")"
                rm -f "${EXPIRED_LOCAL}" "${STEM}.sha256" "${STEM}.manifest.json" "${STEM}.complete" "${STEM}.dump.partial" 2>/dev/null || true
            fi
        done
    fi
fi

# ==============================================================================
# 2. Remote Retention (Daily 30d, Monthly 12m)
# ==============================================================================
if [ "${SCOPE}" = "all" ] || [ "${SCOPE}" = "remote" ]; then
    # Support direct directory testing via REMOTE_DAILY_DIR / REMOTE_MONTHLY_DIR
    if [ -n "${REMOTE_DAILY_DIR:-}" ] && [ -d "${REMOTE_DAILY_DIR}" ]; then
        # Direct Directory Mode (e.g. testing fixtures)
        echo "[REMOTO-TEST] Evaluando retención sobre directorio fixture: ${REMOTE_DAILY_DIR}"

        # Collect all valid owned stems with complete marker
        STEMS=()
        while read -r DUMP_PATH; do
            [ -n "${DUMP_PATH}" ] || continue
            STEM_NAME="$(basename "${DUMP_PATH}" .dump)"
            STEMS+=("${STEM_NAME}")
        done < <(find "${REMOTE_DAILY_DIR}" -name "hato-*-db-*.dump" -type f | sort)

        TOTAL_VALID="${#STEMS[@]}"
        if [ "${TOTAL_VALID}" -eq 0 ]; then
            echo "  No se encontraron copias propias en ${REMOTE_DAILY_DIR}."
        else
            NEWEST_STEM="${STEMS[$((TOTAL_VALID - 1))]}"

            for STEM in "${STEMS[@]}"; do
                DUMP_PATH="${REMOTE_DAILY_DIR}/${STEM}.dump"
                # Check age
                AGE_DAYS=$(( ( $(date +%s) - $(stat -c%Y "${DUMP_PATH}" 2>/dev/null || stat -f%m "${DUMP_PATH}" 2>/dev/null) ) / 86400 ))

                # Check if it should be promoted to monthly
                if [ -n "${REMOTE_MONTHLY_DIR:-}" ]; then
                    MONTHLY_DEST="${REMOTE_MONTHLY_DIR}/${STEM}.dump"
                    if [ ! -f "${MONTHLY_DEST}" ]; then
                        if [ "${DRY_RUN}" -eq 1 ]; then
                            echo "  [DRY-RUN] [PROMOCION-MENSUAL] Copiar a mensual: ${STEM}"
                        else
                            echo "  [PROMOCION-MENSUAL] Copiando a mensual: ${STEM}"
                            cp "${REMOTE_DAILY_DIR}/${STEM}".* "${REMOTE_MONTHLY_DIR}/" 2>/dev/null || true
                        fi
                    fi
                fi

                # Check expiration in daily
                if [ "${AGE_DAYS}" -gt "${RETENTION_DAILY_DAYS}" ]; then
                    if [ "${STEM}" = "${NEWEST_STEM}" ] || [ "${TOTAL_VALID}" -eq 1 ]; then
                        echo "  [REMOTO-PROTEGIDO] Conservando única/última copia válida en daily: ${STEM}"
                    else
                        if [ "${DRY_RUN}" -eq 1 ]; then
                            echo "  [DRY-RUN] [REMOTO-PODA-DAILY] Se eliminaría copia caducada (>30d): ${STEM}"
                        else
                            echo "  [REMOTO-PODA-DAILY] Eliminando copia caducada (>30d): ${STEM}"
                            rm -f "${REMOTE_DAILY_DIR}/${STEM}".* 2>/dev/null || true
                        fi
                    fi
                fi
            done
        fi
    elif [ -f "${RCLONE_CONFIG}" ] && command -v "${RCLONE_CMD}" >/dev/null 2>&1; then
        # Production rclone remote mode
        DAILY_DEST="${RCLONE_REMOTE}:database/prod/daily"
        MONTHLY_DEST="${RCLONE_REMOTE}:database/prod/monthly"

        echo "[REMOTO] Evaluando retención en Google Drive: ${DAILY_DEST}..."

        # List files on daily
        FILES_LIST=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" lsf "${DAILY_DEST}" 2>/dev/null || true)

        # Extract only our owned stems that have a .dump file
        DUMP_FILES=$(echo "${FILES_LIST}" | grep -E '^hato-.*-db-.*\.dump$' | sort || true)
        TOTAL_VALID=$(echo "${DUMP_FILES}" | grep -c . || true)

        if [ "${TOTAL_VALID}" -eq 0 ]; then
            echo "  No se encontraron copias válidas en ${DAILY_DEST}."
        else
            NEWEST_DUMP=$(echo "${DUMP_FILES}" | tail -n 1)
            NEWEST_STEM="${NEWEST_DUMP%.dump}"

            echo "${DUMP_FILES}" | while read -r DUMP_NAME; do
                [ -n "${DUMP_NAME}" ] || continue
                STEM="${DUMP_NAME%.dump}"

                # Promotion to monthly: copy companion files if not exists
                MONTHLY_EXISTS=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" lsf "${MONTHLY_DEST}/${DUMP_NAME}" 2>/dev/null || true)
                if [ -z "${MONTHLY_EXISTS}" ]; then
                    if [ "${DRY_RUN}" -eq 1 ]; then
                        echo "  [DRY-RUN] [PROMOCION-MENSUAL] Promovería ${STEM} a ${MONTHLY_DEST}"
                    else
                        echo "  [PROMOCION-MENSUAL] Promoviendo ${STEM} a ${MONTHLY_DEST}..."
                        "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" copy "${DAILY_DEST}" "${MONTHLY_DEST}" --include "${STEM}.*" 2>/dev/null || true
                    fi
                fi

                # Daily pruning
                if [ "${STEM}" = "${NEWEST_STEM}" ] || [ "${TOTAL_VALID}" -eq 1 ]; then
                    echo "  [REMOTO-PROTEGIDO] Conservando última copia válida en remoto: ${STEM}"
                else
                    # Query age with lsf
                    AGE_SECONDS=$("${RCLONE_CMD}" --config "${RCLONE_CONFIG}" lsf "${DAILY_DEST}/${DUMP_NAME}" --format "t" --time-format "unix" 2>/dev/null || echo 0)
                    NOW_SECONDS=$(date +%s)
                    DIFF_DAYS=$(( (NOW_SECONDS - AGE_SECONDS) / 86400 ))

                    if [ "${DIFF_DAYS}" -gt "${RETENTION_DAILY_DAYS}" ]; then
                        if [ "${DRY_RUN}" -eq 1 ]; then
                            echo "  [DRY-RUN] [REMOTO-PODA-DAILY] Eliminaría ${STEM} de daily (>30d)"
                        else
                            echo "  [REMOTO-PODA-DAILY] Eliminando ${STEM} de daily (>30d)..."
                            "${RCLONE_CMD}" --config "${RCLONE_CONFIG}" delete "${DAILY_DEST}" --include "${STEM}.*" 2>/dev/null || true
                        fi
                    fi
                fi
            done
        fi
    fi
fi

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Proceso de retención finalizado."
