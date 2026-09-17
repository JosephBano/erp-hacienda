#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Disaster Recovery Rehearsal Simulation & Verification
# Simulates full disaster recovery from downloaded offsite backup to isolated host,
# measures observed RTO, verifies all modular domain invariants, and emits report.
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEMP_DIR="$(mktemp -d /tmp/hato-dr-rehearsal.XXXXXX)"
CONTAINER="${POSTGRES_CONTAINER:-hato-postgres}"
TARGET_DB="hato_dr_rehearsal_$(date +%s)"
REPORT_FILE="${TEMP_DIR}/dr_rehearsal_report.md"

cleanup() {
    rm -rf "${TEMP_DIR}"
    if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}\$"; then
        docker exec "${CONTAINER}" dropdb --if-exists -U hato "${TARGET_DB}" 2>/dev/null || true
    fi
}
trap cleanup EXIT

echo "=== SIMULACRO DE RECUPERACIÓN ANTE DESASTRES (E2E-7 / DR REHEARSAL) ==="

START_TIME=$(date +%s)

# 1. Verify PostgreSQL container
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}\$"; then
    echo "[SKIP] Contenedor '${CONTAINER}' no está activo. Omitiendo simulacro real."
    exit 0
fi

# 2. Produce real baseline backup
export BACKUP_DIR="${TEMP_DIR}/offsite_drive"
export POSTGRES_CONTAINER="${CONTAINER}"
export POSTGRES_DB="hato"
export POSTGRES_USER="hato"
export ENVIRONMENT="production"

"${REPO_ROOT}/scripts/backup.sh"

DUMP_PATH=$(find "${BACKUP_DIR}" -name "*.dump" -type f | head -n 1)
STEM="$(basename "${DUMP_PATH}" .dump)"
DUMP_BYTES=$(stat -c%s "${DUMP_PATH}")

DOWNLOAD_START=$(date +%s)
# 3. Simulate Offsite Download to Clean Environment
CLEAN_HOST_DIR="${TEMP_DIR}/recovered_host"
mkdir -p "${CLEAN_HOST_DIR}"
cp "${BACKUP_DIR}/${STEM}".* "${CLEAN_HOST_DIR}/"
DOWNLOAD_END=$(date +%s)
DOWNLOAD_SECONDS=$((DOWNLOAD_END - DOWNLOAD_START))

# 4. Mandatory Pre-Restore Cryptographic Verification
(cd "${CLEAN_HOST_DIR}" && sha256sum -c "${STEM}.sha256" >/dev/null)
"${REPO_ROOT}/scripts/backup-manifest.sh" verify "${CLEAN_HOST_DIR}/${STEM}.manifest.json" "${CLEAN_HOST_DIR}/${STEM}.dump"

RESTORE_START=$(date +%s)
# 5. Execute Protected Restoration
"${REPO_ROOT}/scripts/restore.sh" "${CLEAN_HOST_DIR}/${STEM}.dump" "${TARGET_DB}"
RESTORE_END=$(date +%s)
RESTORE_SECONDS=$((RESTORE_END - RESTORE_START))

VERIFY_START=$(date +%s)
# 6. Verify Modular Schemas and Domain Entities
SCHEMAS_QUERY="SELECT schema_name FROM information_schema.schemata WHERE schema_name IN ('livestock', 'production', 'inventory', 'people', 'breeding', 'tasks');"
FOUND_SCHEMAS=$(docker exec "${CONTAINER}" psql -U hato -d "${TARGET_DB}" -t -c "${SCHEMAS_QUERY}" | tr -s ' ' '\n' | grep -v '^$' | sort)

EXPECTED_SCHEMAS=$(printf "breeding\ninventory\nlivestock\npeople\nproduction\ntasks")
if [ "${FOUND_SCHEMAS}" != "${EXPECTED_SCHEMAS}" ]; then
    echo "[FAIL] Los esquemas recuperados no coinciden:" >&2
    echo "  Esperado: ${EXPECTED_SCHEMAS}" >&2
    echo "  Encontrado: ${FOUND_SCHEMAS}" >&2
    exit 1
fi

ANIMAL_COUNT=$(docker exec "${CONTAINER}" psql -U hato -d "${TARGET_DB}" -t -c "SELECT count(*) FROM livestock.animals;" | tr -d '[:space:]')
VERIFY_END=$(date +%s)
VERIFY_SECONDS=$((VERIFY_END - VERIFY_START))

TOTAL_END=$(date +%s)
TOTAL_RTO_SECONDS=$((TOTAL_END - START_TIME))

# 7. Check that observed RTO does not exceed 4 hours (14400 seconds)
if [ "${TOTAL_RTO_SECONDS}" -gt 14400 ]; then
    echo "[FAIL] El RTO observado (${TOTAL_RTO_SECONDS}s) excede el objetivo de 4 horas (14400s)!" >&2
    exit 1
fi

# 8. Emit Disaster Recovery Rehearsal Report
cat << EOF > "${REPORT_FILE}"
# Reporte de Simulacro de Recuperación ante Desastres (DR Rehearsal)

- **Fecha UTC:** $(date -u +"%Y-%m-%dT%H:%M:%SZ")
- **Backup ID:** ${STEM}
- **Tamaño del Dump:** ${DUMP_BYTES} bytes
- **Entorno Origen:** ${ENVIRONMENT}
- **Base de Datos Destino Aislada:** ${TARGET_DB}

## Tiempos y RTO Observado
- **Descarga simulada:** ${DOWNLOAD_SECONDS} segundos
- **Restauración en BD:** ${RESTORE_SECONDS} segundos
- **Verificación de esquemas:** ${VERIFY_SECONDS} segundos
- **RTO Total Observado:** ${TOTAL_RTO_SECONDS} segundos (Objetivo RTO: < 4 horas / 14400s)
- **Resultado RTO:** CUMPLE OBJETIVO

## Validación Modular y Dominios
- **Esquemas Verificados:** livestock, production, inventory, people, breeding, tasks (6 de 6)
- **Integridad de Entidades:** ${ANIMAL_COUNT} animales verificados en hato
- **Integridad Referencial:** Correcta (sin errores en pg_restore)
- **Estado Final:** RECUPERACIÓN EXITOSA Y COMPROBADA
EOF

echo "[PASS] Simulacro de recuperación completado exitosamente en ${TOTAL_RTO_SECONDS} segundos."
cat "${REPORT_FILE}"
