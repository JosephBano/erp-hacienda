#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda - Backup Manifest Generator and Validator
# ==============================================================================

ACTION="${1:-}"

usage() {
    cat <<EOF
Uso:
  $0 generate <dump_file> <output_manifest> [environment] [database] [release_sha]
  $0 verify <manifest_file> [dump_file]
  $0 read <manifest_file> <field_name>
EOF
    exit 1
}

if [ -z "${ACTION}" ]; then
    usage
fi

case "${ACTION}" in
    generate)
        DUMP_FILE="${2:-}"
        OUTPUT_MANIFEST="${3:-}"
        ENVIRONMENT="${4:-production}"
        DATABASE="${5:-hato_production}"
        RELEASE_SHA="${6:-}"

        if [ -z "${DUMP_FILE}" ] || [ -z "${OUTPUT_MANIFEST}" ]; then
            echo "[ERROR] 'generate' requiere <dump_file> y <output_manifest>" >&2
            exit 1
        fi

        if [ ! -f "${DUMP_FILE}" ]; then
            echo "[ERROR] El archivo de dump '${DUMP_FILE}' no existe." >&2
            exit 1
        fi

        if [[ "${DUMP_FILE}" == *.partial ]]; then
            echo "[ERROR] No se puede generar manifiesto para un archivo .partial." >&2
            exit 1
        fi

        DUMP_BASENAME="$(basename "${DUMP_FILE}")"
        DUMP_DIR="$(cd "$(dirname "${DUMP_FILE}")" && pwd)"
        STEM="${DUMP_BASENAME%.dump}"

        # Calculate SHA-256 relative to directory
        SHA256_HASH=$(cd "${DUMP_DIR}" && sha256sum "${DUMP_BASENAME}" | awk '{print $1}')
        DUMP_BYTES=$(stat -c%s "${DUMP_FILE}" 2>/dev/null || stat -f%z "${DUMP_FILE}" 2>/dev/null)
        CREATED_AT=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

        if [ -z "${RELEASE_SHA}" ]; then
            RELEASE_SHA="$(git rev-parse HEAD 2>/dev/null || echo "unversioned")"
        fi

        python3 -c "
import json, sys

data = {
    'manifest_version': '1.0',
    'id': sys.argv[1],
    'environment': sys.argv[2],
    'database': sys.argv[3],
    'created_at_utc': sys.argv[4],
    'postgres_version': '16',
    'release_sha': sys.argv[5],
    'dump_file': sys.argv[6],
    'dump_bytes': int(sys.argv[7]),
    'sha256': sys.argv[8],
    'schemas': ['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks'],
    'verification_status': 'verified',
    'key_version': 'v1'
}

with open(sys.argv[9], 'w') as f:
    json.dump(data, f, indent=2)
" "${STEM}" "${ENVIRONMENT}" "${DATABASE}" "${CREATED_AT}" "${RELEASE_SHA}" "${DUMP_BASENAME}" "${DUMP_BYTES}" "${SHA256_HASH}" "${OUTPUT_MANIFEST}"

        echo "[OK] Manifiesto generado exitosamente: ${OUTPUT_MANIFEST}"
        ;;

    verify)
        MANIFEST_FILE="${2:-}"
        DUMP_FILE="${3:-}"

        if [ -z "${MANIFEST_FILE}" ] || [ ! -f "${MANIFEST_FILE}" ]; then
            echo "[ERROR] Archivo de manifiesto '${MANIFEST_FILE}' no existe." >&2
            exit 1
        fi

        # Extract fields
        read -r EXPECTED_DUMP EXPECTED_HASH EXPECTED_BYTES < <(python3 -c "
import json, sys
with open(sys.argv[1]) as f:
    m = json.load(f)
print(f\"{m.get('dump_file', '')} {m.get('sha256', '')} {m.get('dump_bytes', 0)}\")
" "${MANIFEST_FILE}")

        if [ -z "${DUMP_FILE}" ]; then
            MANIFEST_DIR="$(cd "$(dirname "${MANIFEST_FILE}")" && pwd)"
            DUMP_FILE="${MANIFEST_DIR}/${EXPECTED_DUMP}"
        fi

        if [ ! -f "${DUMP_FILE}" ]; then
            echo "[ERROR] Dump '${DUMP_FILE}' referenciado en el manifiesto no existe." >&2
            exit 1
        fi

        if [[ "${DUMP_FILE}" == *.partial ]]; then
            echo "[ERROR] El dump '${DUMP_FILE}' es un archivo .partial." >&2
            exit 1
        fi

        DUMP_DIR="$(cd "$(dirname "${DUMP_FILE}")" && pwd)"
        ACTUAL_HASH=$(cd "${DUMP_DIR}" && sha256sum "$(basename "${DUMP_FILE}")" | awk '{print $1}')
        ACTUAL_BYTES=$(stat -c%s "${DUMP_FILE}" 2>/dev/null || stat -f%z "${DUMP_FILE}" 2>/dev/null)

        if [ "${ACTUAL_HASH}" != "${EXPECTED_HASH}" ]; then
            echo "[ERROR] Checksum SHA256 no coincide con el manifiesto!" >&2
            echo "  Esperado: ${EXPECTED_HASH}" >&2
            echo "  Actual:   ${ACTUAL_HASH}" >&2
            exit 1
        fi

        if [ "${ACTUAL_BYTES}" -ne "${EXPECTED_BYTES}" ]; then
            echo "[ERROR] Tamaño en bytes no coincide con el manifiesto!" >&2
            echo "  Esperado: ${EXPECTED_BYTES}" >&2
            echo "  Actual:   ${ACTUAL_BYTES}" >&2
            exit 1
        fi

        echo "[OK] Manifiesto verificado correctamente contra dump: ${DUMP_FILE}"
        ;;

    read)
        MANIFEST_FILE="${2:-}"
        FIELD="${3:-}"

        if [ -z "${MANIFEST_FILE}" ] || [ ! -f "${MANIFEST_FILE}" ] || [ -z "${FIELD}" ]; then
            echo "[ERROR] 'read' requiere <manifest_file> existente y <field_name>" >&2
            exit 1
        fi

        python3 -c "
import json, sys
with open(sys.argv[1]) as f:
    m = json.load(f)
val = m.get(sys.argv[2], '')
if isinstance(val, (dict, list)):
    print(json.dumps(val))
else:
    print(val)
" "${MANIFEST_FILE}" "${FIELD}"
        ;;

    *)
        echo "[ERROR] Acción desconocida: ${ACTION}" >&2
        usage
        ;;
esac
