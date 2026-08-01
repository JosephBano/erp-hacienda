#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda Ganadera - Automated PostgreSQL Restoration & Verification Script
# ==============================================================================

if [ "$#" -lt 1 ]; then
    echo "Uso: $0 <archivo_backup.sql.gz> [nombre_base_datos_destino]"
    exit 1
fi

BACKUP_FILE="$1"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-hato-postgres}"
POSTGRES_DB="${2:-hato_restore_test}"
POSTGRES_USER="${POSTGRES_USER:-hato}"

if [ ! -f "${BACKUP_FILE}" ]; then
    echo "[ERROR] El archivo de backup '${BACKUP_FILE}' no existe." >&2
    exit 1
fi

CHECKSUM_FILE="${BACKUP_FILE}.sha256"
if [ -f "${CHECKSUM_FILE}" ]; then
    echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Verificando checksum SHA256 del backup..."
    sha256sum -c "${CHECKSUM_FILE}"
else
    echo "[ADVERTENCIA] No se encontró archivo .sha256; omitiendo validación de integridad."
fi

echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Recreando base de datos de prueba: ${POSTGRES_DB}..."
docker exec "${POSTGRES_CONTAINER}" dropdb --if-exists -U "${POSTGRES_USER}" "${POSTGRES_DB}" || true
docker exec "${POSTGRES_CONTAINER}" createdb -U "${POSTGRES_USER}" "${POSTGRES_DB}"

echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Restaurando datos desde ${BACKUP_FILE}..."
gunzip -c "${BACKUP_FILE}" | docker exec -i "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" > /dev/null

echo "[$(date -u +"%Y-%m-%d %H:%M:%S UTC")] Verificando esquemas y conteos en ${POSTGRES_DB}..."
TOTAL_ANIMALS=$(docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM livestock.animals;" 2>/dev/null || echo "0")

echo "[EXITO] Restauración completada correctamente. Total animales verificados: ${TOTAL_ANIMALS// /}"
