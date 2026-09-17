#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Integration Test: Real PostgreSQL 16 Backup & Restore Cycle
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEMP_DIR="$(mktemp -d /tmp/hato-real-backup-restore.XXXXXX)"
RESTORE_DB="hato_backup_test_$(date +%s)"
CONTAINER="${POSTGRES_CONTAINER:-hato-postgres}"

cleanup() {
    echo "Limpiando recursos de prueba..."
    rm -rf "${TEMP_DIR}"
    if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}\$"; then
        docker exec "${CONTAINER}" dropdb --if-exists -U hato "${RESTORE_DB}" 2>/dev/null || true
    fi
}
trap cleanup EXIT

echo "=== 1. Verificando disponibilidad de PostgreSQL ==="
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}\$"; then
    echo "[SKIP] El contenedor '${CONTAINER}' no está en ejecución. Omitiendo prueba con BD real."
    exit 0
fi

docker exec "${CONTAINER}" pg_isready -U hato -d hato

echo "=== 2. Ejecutando scripts/backup.sh ==="
export BACKUP_DIR="${TEMP_DIR}/backups"
export POSTGRES_CONTAINER="${CONTAINER}"
export POSTGRES_DB="hato"
export POSTGRES_USER="hato"
export ENVIRONMENT="test"

"${REPO_ROOT}/scripts/backup.sh"

echo "=== 3. Validando artefactos generados ==="
DUMP_FILE=$(find "${BACKUP_DIR}" -name "*.dump" -type f | head -n 1)
SHA_FILE="${DUMP_FILE%.dump}.sha256"
MANIFEST_FILE="${DUMP_FILE%.dump}.manifest.json"

[ -f "${DUMP_FILE}" ] || { echo "[FAIL] No se encontró archivo .dump"; exit 1; }
[ -f "${SHA_FILE}" ] || { echo "[FAIL] No se encontró archivo .sha256"; exit 1; }
[ -f "${MANIFEST_FILE}" ] || { echo "[FAIL] No se encontró archivo .manifest.json"; exit 1; }

# Validar formato custom de PostgreSQL (magic header PGDMP)
MAGIC=$(head -c 5 "${DUMP_FILE}")
if [ "${MAGIC}" != "PGDMP" ]; then
    echo "[FAIL] El archivo dump no tiene cabecera custom PGDMP: '${MAGIC}'" >&2
    exit 1
fi
echo "[PASS] Formato de dump verificado: PostgreSQL Custom (PGDMP)"

# Validar permisos 600
PERMS=$(stat -c%a "${DUMP_FILE}")
if [ "${PERMS}" != "600" ]; then
    echo "[FAIL] Permisos de dump no son 600: ${PERMS}" >&2
    exit 1
fi
echo "[PASS] Permisos estrictos verificados: 600"

# Validar manifiesto
"${REPO_ROOT}/scripts/backup-manifest.sh" verify "${MANIFEST_FILE}" "${DUMP_FILE}"
echo "[PASS] Manifiesto técnico verificado contra dump"

echo "=== 4. Ejecutando scripts/restore.sh en base aislada ==="
export POSTGRES_USER="hato"
"${REPO_ROOT}/scripts/restore.sh" "${DUMP_FILE}" "${RESTORE_DB}"

echo "=== 5. Comprobando esquemas y datos en la base restaurada ==="
SCHEMAS=$(docker exec "${CONTAINER}" psql -U hato -d "${RESTORE_DB}" -t -c "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('livestock', 'production', 'inventory', 'people', 'breeding', 'tasks');" | tr -d '[:space:]')
if [ "${SCHEMAS}" -ne 6 ]; then
    echo "[FAIL] Se esperaban 6 esquemas modulares, encontrados: ${SCHEMAS}" >&2
    exit 1
fi
echo "[PASS] Los 6 esquemas modulares existen en la base restaurada."

echo "=== PRUEBA DE CICLO REAL COMPLETADA EXITOSAMENTE ==="
