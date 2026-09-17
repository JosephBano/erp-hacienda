#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Automated PostgreSQL Restoration & Verification Script
# Format: Custom (-Fc), Strict Checksum & Manifest Verification, Fail-Closed
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

usage() {
    cat <<EOF
Uso: $0 <archivo_backup.dump> [nombre_base_datos_destino] [opciones]

Opciones:
  --force-production-restore-disaster-only   Permite restaurar en hato_production en caso de desastre real
EOF
    exit 1
}

if [ "$#" -lt 1 ]; then
    usage
fi

BACKUP_FILE="$1"
shift

TARGET_DB=""
FORCE_PRODUCTION=0

while [ "$#" -gt 0 ]; do
    case "$1" in
        --force-production-restore-disaster-only)
            FORCE_PRODUCTION=1
            shift
            ;;
        *)
            if [ -z "${TARGET_DB}" ]; then
                TARGET_DB="$1"
            fi
            shift
            ;;
    esac
done

if [ -z "${TARGET_DB}" ]; then
    TARGET_DB="hato_restore_test_$(date -u +"%Y%m%d_%H%M%SZ")"
fi

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-hato-postgres}"
POSTGRES_USER="${POSTGRES_USER:-${PRODUCTION_POSTGRES_OWNER_USER:-hato}}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-${PRODUCTION_POSTGRES_OWNER_PASSWORD:-}}"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"

# 1. Reject Non-Existent or Partial Files
if [ ! -f "${BACKUP_FILE}" ]; then
    echo "[ERROR] El archivo de backup '${BACKUP_FILE}' no existe." >&2
    exit 1
fi

if [[ "${BACKUP_FILE}" == *.partial ]]; then
    echo "[ERROR] Rechazando restauración de archivo temporal .partial." >&2
    exit 1
fi

# 2. Production Database Safeguard
if [ "${TARGET_DB}" = "hato_production" ] || [ "${TARGET_DB}" = "hato_prod" ]; then
    if [ "${FORCE_PRODUCTION}" -ne 1 ]; then
        echo "[ERROR] Rechazando restauración sobre base de datos de producción ('${TARGET_DB}')." >&2
        echo "        Para recuperación ante desastres real, debe pasar la bandera explícita:" >&2
        echo "        --force-production-restore-disaster-only" >&2
        exit 1
    fi
    echo "[ALERTA CRITICA] Ejecutando restauración sobre base de datos de producción bajo confirmación explícita." >&2
fi

# 3. Mandatory Checksum Verification (Never Skipped)
CHECKSUM_FILE="${BACKUP_FILE%.dump}.sha256"
if [ ! -f "${CHECKSUM_FILE}" ]; then
    if [ -f "${BACKUP_FILE}.sha256" ]; then
        CHECKSUM_FILE="${BACKUP_FILE}.sha256"
    else
        echo "[ERROR] No se encontró archivo de verificación '${CHECKSUM_FILE}'. La validación de integridad es obligatoria." >&2
        exit 1
    fi
fi

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificando checksum SHA256..."
DUMP_DIR="$(cd "$(dirname "${BACKUP_FILE}")" && pwd)"
DUMP_BASENAME="$(basename "${BACKUP_FILE}")"
CHECKSUM_BASENAME="$(basename "${CHECKSUM_FILE}")"

if ! (cd "${DUMP_DIR}" && sha256sum -c "${CHECKSUM_BASENAME}" >/dev/null 2>&1); then
    echo "[ERROR] La verificación del checksum SHA256 falló. El archivo de backup está corrupto o fue modificado." >&2
    exit 1
fi
echo "[OK] Checksum SHA256 verificado correctamente."

# 4. Optional Manifest Verification if Present
MANIFEST_FILE="${BACKUP_FILE%.dump}.manifest.json"
if [ -f "${MANIFEST_FILE}" ]; then
    echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificando manifiesto de respaldo..."
    MANIFEST_SCRIPT="${SCRIPT_DIR}/backup-manifest.sh"
    if [ -x "${MANIFEST_SCRIPT}" ]; then
        if ! "${MANIFEST_SCRIPT}" verify "${MANIFEST_FILE}" "${BACKUP_FILE}"; then
            echo "[ERROR] La verificación del manifiesto falló." >&2
            exit 1
        fi
    fi
fi

# Helper functions for database execution
exec_psql() {
    local SQL="$1"
    if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
        docker exec -i "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -v ON_ERROR_STOP=1 -c "${SQL}"
    else
        PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -v ON_ERROR_STOP=1 -c "${SQL}"
    fi
}

exec_pg_restore() {
    local DB="$1"
    local FILE="$2"
    if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
        docker exec -i "${POSTGRES_CONTAINER}" pg_restore --exit-on-error --no-owner --no-acl -U "${POSTGRES_USER}" -d "${DB}" < "${FILE}"
    else
        PGPASSWORD="${POSTGRES_PASSWORD}" pg_restore -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" --exit-on-error --no-owner --no-acl -d "${DB}" "${FILE}"
    fi
}

# 5. Clean Database Recreation
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Preparando base de datos destino limpia: ${TARGET_DB}..."
if [ "${FORCE_PRODUCTION}" -ne 1 ]; then
    # For test databases, drop and recreate cleanly
    if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
        docker exec "${POSTGRES_CONTAINER}" dropdb --if-exists -U "${POSTGRES_USER}" "${TARGET_DB}"
        docker exec "${POSTGRES_CONTAINER}" createdb -U "${POSTGRES_USER}" "${TARGET_DB}"
    else
        PGPASSWORD="${POSTGRES_PASSWORD}" dropdb --if-exists -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" "${TARGET_DB}"
        PGPASSWORD="${POSTGRES_PASSWORD}" createdb -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" "${TARGET_DB}"
    fi
fi

# 6. Perform Restoration using pg_restore with fail-closed behavior
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Restaurando datos desde ${BACKUP_FILE} hacia ${TARGET_DB}..."
if ! exec_pg_restore "${TARGET_DB}" "${BACKUP_FILE}"; then
    echo "[ERROR] pg_restore falló durante la restauración. Abortando con código de error." >&2
    exit 1
fi

# 7. Verification of Schemas and Records
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Verificando esquemas y registros en ${TARGET_DB}..."

# Verify modular schemas exist
QUERY_SCHEMAS="SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('livestock', 'production', 'inventory', 'people', 'breeding', 'tasks');"
if command -v docker >/dev/null 2>&1 && docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${POSTGRES_CONTAINER}\$"; then
    SCHEMA_COUNT=$(docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -d "${TARGET_DB}" -v ON_ERROR_STOP=1 -t -c "${QUERY_SCHEMAS}" | tr -d '[:space:]')
    ANIMAL_COUNT=$(docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -d "${TARGET_DB}" -v ON_ERROR_STOP=1 -t -c "SELECT count(*) FROM livestock.animals;" | tr -d '[:space:]')
else
    SCHEMA_COUNT=$(PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${TARGET_DB}" -v ON_ERROR_STOP=1 -t -c "${QUERY_SCHEMAS}" | tr -d '[:space:]')
    ANIMAL_COUNT=$(PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${TARGET_DB}" -v ON_ERROR_STOP=1 -t -c "SELECT count(*) FROM livestock.animals;" | tr -d '[:space:]')
fi

if [ -z "${SCHEMA_COUNT}" ] || [ "${SCHEMA_COUNT}" -lt 1 ]; then
    echo "[ERROR] Verificación fallida: no se encontraron los esquemas del dominio en la base restaurada." >&2
    exit 1
fi

echo "[EXITO] Restauración completada y verificada correctamente."
echo "  Base destino:        ${TARGET_DB}"
echo "  Esquemas detectados: ${SCHEMA_COUNT}"
echo "  Animales en hato:    ${ANIMAL_COUNT}"
