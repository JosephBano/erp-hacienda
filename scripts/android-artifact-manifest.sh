#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda - Android Artifact Manifest Generator and Validator
# ==============================================================================

ACTION="${1:-}"

usage() {
    cat <<EOF
Uso:
  $0 generate <apk_file> <output_manifest> [channel] [commit_sha] [version_code] [version] [package_name] [target_api_url] [build_request_id] [release_tag] [fingerprint]
  $0 verify <manifest_file> [apk_file]
  $0 read <manifest_file> <field_name>
EOF
    exit 1
}

if [ -z "${ACTION}" ]; then
    usage
fi

case "${ACTION}" in
    generate)
        APK_FILE="${2:-}"
        OUTPUT_MANIFEST="${3:-}"
        CHANNEL="${4:-stage}"
        COMMIT_SHA="${5:-}"
        VERSION_CODE="${6:-1}"
        VERSION="${7:-1.0.0}"
        PACKAGE_NAME="${8:-com.joemandev.hatofieldapp.stage}"
        TARGET_API_URL="${9:-https://joemanserver.ts.net/api}"
        BUILD_REQUEST_ID="${10:-}"
        RELEASE_TAG="${11:-}"
        FINGERPRINT="${12:-}"

        if [ -z "${APK_FILE}" ] || [ -z "${OUTPUT_MANIFEST}" ]; then
            echo "[ERROR] 'generate' requiere <apk_file> y <output_manifest>" >&2
            exit 1
        fi

        if [ ! -f "${APK_FILE}" ]; then
            echo "[ERROR] El archivo APK '${APK_FILE}' no existe." >&2
            exit 1
        fi

        APK_BASENAME="$(basename "${APK_FILE}")"
        APK_DIR="$(cd "$(dirname "${APK_FILE}")" && pwd)"

        # Calculate SHA-256
        SHA256_HASH=$(cd "${APK_DIR}" && sha256sum "${APK_BASENAME}" | awk '{print $1}')
        APK_BYTES=$(stat -c%s "${APK_FILE}" 2>/dev/null || stat -f%z "${APK_FILE}" 2>/dev/null)
        CREATED_AT=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

        if [ -z "${COMMIT_SHA}" ]; then
            COMMIT_SHA="$(git rev-parse HEAD 2>/dev/null || echo "unversioned")"
        fi

        # If fingerprint not provided, try to extract via keytool or apksigner
        if [ -z "${FINGERPRINT}" ] && command -v keytool >/dev/null 2>&1; then
            FINGERPRINT=$(keytool -printcert -jarfile "${APK_FILE}" 2>/dev/null \
                | grep -i "SHA256:" \
                | head -n1 \
                | sed 's/.*SHA256: *//I' \
                | tr -d ' ' || true)
        fi

        python3 -c "
import json, sys

data = {
    'manifest_version': '1.0',
    'build_request_id': sys.argv[1],
    'channel': sys.argv[2],
    'version': sys.argv[3],
    'version_code': int(sys.argv[4]),
    'package_name': sys.argv[5],
    'target_api_url': sys.argv[6],
    'commit_sha': sys.argv[7],
    'release_tag': sys.argv[8],
    'artifact_file_name': sys.argv[9],
    'artifact_size_bytes': int(sys.argv[10]),
    'sha256': sys.argv[11],
    'signing_certificate_fingerprint': sys.argv[12],
    'created_at_utc': sys.argv[13]
}

with open(sys.argv[14], 'w') as f:
    json.dump(data, f, indent=2)
" "${BUILD_REQUEST_ID}" "${CHANNEL}" "${VERSION}" "${VERSION_CODE}" "${PACKAGE_NAME}" "${TARGET_API_URL}" "${COMMIT_SHA}" "${RELEASE_TAG}" "${APK_BASENAME}" "${APK_BYTES}" "${SHA256_HASH}" "${FINGERPRINT}" "${CREATED_AT}" "${OUTPUT_MANIFEST}"

        echo "[OK] Manifiesto de artefacto Android generado: ${OUTPUT_MANIFEST}"
        ;;

    verify)
        MANIFEST_FILE="${2:-}"
        APK_FILE="${3:-}"

        if [ -z "${MANIFEST_FILE}" ] || [ ! -f "${MANIFEST_FILE}" ]; then
            echo "[ERROR] Archivo de manifiesto '${MANIFEST_FILE}' no existe." >&2
            exit 1
        fi

        # Extract fields from manifest
        read -r EXPECTED_APK EXPECTED_HASH EXPECTED_BYTES EXPECTED_FP < <(python3 -c "
import json, sys
with open(sys.argv[1]) as f:
    m = json.load(f)
print(f\"{m.get('artifact_file_name', '')} {m.get('sha256', '')} {m.get('artifact_size_bytes', 0)} {m.get('signing_certificate_fingerprint', '')}\")
" "${MANIFEST_FILE}")

        if [ -z "${APK_FILE}" ]; then
            MANIFEST_DIR="$(cd "$(dirname "${MANIFEST_FILE}")" && pwd)"
            APK_FILE="${MANIFEST_DIR}/${EXPECTED_APK}"
        fi

        if [ ! -f "${APK_FILE}" ]; then
            echo "[ERROR] APK '${APK_FILE}' referenciado en el manifiesto no existe." >&2
            exit 1
        fi

        APK_DIR="$(cd "$(dirname "${APK_FILE}")" && pwd)"
        ACTUAL_HASH=$(cd "${APK_DIR}" && sha256sum "$(basename "${APK_FILE}")" | awk '{print $1}')
        ACTUAL_BYTES=$(stat -c%s "${APK_FILE}" 2>/dev/null || stat -f%z "${APK_FILE}" 2>/dev/null)

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

        if [ -n "${EXPECTED_FP}" ] && command -v keytool >/dev/null 2>&1; then
            ACTUAL_FP=$(keytool -printcert -jarfile "${APK_FILE}" 2>/dev/null \
                | grep -i "SHA256:" \
                | head -n1 \
                | sed 's/.*SHA256: *//I' \
                | tr -d ' ' || true)
            if [ -n "${ACTUAL_FP}" ] && [ "${ACTUAL_FP^^}" != "${EXPECTED_FP^^}" ]; then
                echo "[ERROR] Fingerprint de certificado no coincide con el manifiesto!" >&2
                echo "  Esperado: ${EXPECTED_FP}" >&2
                echo "  Actual:   ${ACTUAL_FP}" >&2
                exit 1
            fi
        fi

        echo "[OK] Manifiesto verificado correctamente contra APK: ${APK_FILE}"
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
val = m.get(sys.argv[2])
if val is not None:
    print(val)
else:
    sys.exit(1)
" "${MANIFEST_FILE}" "${FIELD}"
        ;;

    *)
        echo "[ERROR] Acción desconocida: '${ACTION}'" >&2
        usage
        ;;
esac
