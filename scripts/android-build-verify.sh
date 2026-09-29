#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda - Android Build Verifier
# ==============================================================================
# Valida integridad, empaquetado, firma y metadatos de un APK generado antes
# de su catalogación o publicación.
# ==============================================================================

APK_FILE="${1:-}"
EXPECTED_CHANNEL="${2:-}"
EXPECTED_PACKAGE="${3:-}"
EXPECTED_VERSION_CODE="${4:-}"
EXPECTED_VERSION="${5:-}"
EXPECTED_FINGERPRINT="${6:-}"

usage() {
    cat <<EOF
Uso:
  $0 <apk_file> <expected_channel> <expected_package> <expected_version_code> [expected_version] [expected_fingerprint]

Ejemplo:
  $0 hato-1.0.0-stage-b100-abc.apk stage com.joemandev.hatofieldapp.stage 100 1.0.0
EOF
    exit 1
}

if [ -z "${APK_FILE}" ] || [ -z "${EXPECTED_CHANNEL}" ] || [ -z "${EXPECTED_PACKAGE}" ] || [ -z "${EXPECTED_VERSION_CODE}" ]; then
    usage
fi

if [ ! -f "${APK_FILE}" ]; then
    echo "[ERROR] El archivo APK '${APK_FILE}' no existe." >&2
    exit 1
fi

echo "==> Verificando APK: ${APK_FILE}"

# 1. Validar integridad física como archivo zip
if ! unzip -tq "${APK_FILE}" >/dev/null 2>&1; then
    echo "[ERROR] El archivo '${APK_FILE}' no es un archivo ZIP/APK válido o está corrupto." >&2
    exit 1
fi
echo "  [OK] Estructura ZIP válida."

# 2. Validar presencia de firma y certificado con keytool
if command -v keytool >/dev/null 2>&1; then
    CERT_OUTPUT=$(keytool -printcert -jarfile "${APK_FILE}" 2>/dev/null || true)
    if [ -z "${CERT_OUTPUT}" ]; then
        echo "[ERROR] El APK '${APK_FILE}' no contiene un certificado de firma válido." >&2
        exit 1
    fi

    # Verificar si es certificado debug
    if [[ "${EXPECTED_CHANNEL}" =~ ^(prod|production)$ ]]; then
        if echo "${CERT_OUTPUT}" | grep -iq "CN=Android Debug"; then
            echo "[ERROR] El APK de producción está firmado con una clave Android Debug! Rechazado." >&2
            exit 1
        fi
    fi

    ACTUAL_FP=$(echo "${CERT_OUTPUT}" | grep -i "SHA256:" | head -n1 | sed 's/.*SHA256: *//I' | tr -d ' ' || true)
    if [ -n "${EXPECTED_FINGERPRINT}" ] && [ -n "${ACTUAL_FP}" ]; then
        if [ "${ACTUAL_FP^^}" != "${EXPECTED_FINGERPRINT^^}" ]; then
            echo "[ERROR] Fingerprint del certificado no coincide." >&2
            echo "  Esperado: ${EXPECTED_FINGERPRINT}" >&2
            echo "  Actual:   ${ACTUAL_FP}" >&2
            exit 1
        fi
        echo "  [OK] Fingerprint de certificado coincide: ${ACTUAL_FP}"
    else
        echo "  [OK] Certificado verificado. Fingerprint: ${ACTUAL_FP:-N/A}"
    fi
else
    echo "  [WARN] 'keytool' no encontrado; omitiendo verificación profunda de certificado X.509."
fi

# 3. Validar package, versionCode y debuggable con aapt / aapt2 si está disponible
AAPT_BIN=""
if command -v aapt >/dev/null 2>&1; then
    AAPT_BIN="aapt"
elif [ -n "${ANDROID_HOME:-}" ] && [ -d "${ANDROID_HOME}/build-tools" ]; then
    LATEST_AAPT=$(find "${ANDROID_HOME}/build-tools" -name aapt -type f -perm /111 2>/dev/null | sort -V | tail -n1 || true)
    if [ -n "${LATEST_AAPT}" ]; then
        AAPT_BIN="${LATEST_AAPT}"
    fi
fi

if [ -n "${AAPT_BIN}" ]; then
    BADGING=$("${AAPT_BIN}" dump badging "${APK_FILE}" 2>/dev/null || true)
    if [ -n "${BADGING}" ]; then
        # package: name='com.joemandev.hatofieldapp' versionCode='100' versionName='1.0.0'
        PKG_LINE=$(echo "${BADGING}" | grep "^package:" || true)
        ACTUAL_PKG=$(echo "${PKG_LINE}" | sed -n "s/.*name='\([^']*\)'.*/\1/p")
        ACTUAL_CODE=$(echo "${PKG_LINE}" | sed -n "s/.*versionCode='\([^']*\)'.*/\1/p")
        ACTUAL_VER=$(echo "${PKG_LINE}" | sed -n "s/.*versionName='\([^']*\)'.*/\1/p")

        if [ "${ACTUAL_PKG}" != "${EXPECTED_PACKAGE}" ]; then
            echo "[ERROR] Package name no coincide." >&2
            echo "  Esperado: ${EXPECTED_PACKAGE}" >&2
            echo "  Actual:   ${ACTUAL_PKG}" >&2
            exit 1
        fi
        echo "  [OK] Package name correcto: ${ACTUAL_PKG}"

        if [ "${ACTUAL_CODE}" != "${EXPECTED_VERSION_CODE}" ]; then
            echo "[ERROR] VersionCode no coincide." >&2
            echo "  Esperado: ${EXPECTED_VERSION_CODE}" >&2
            echo "  Actual:   ${ACTUAL_CODE}" >&2
            exit 1
        fi
        echo "  [OK] VersionCode correcto: ${ACTUAL_CODE}"

        if [ -n "${EXPECTED_VERSION}" ] && [ -n "${ACTUAL_VER}" ]; then
            if [ "${ACTUAL_VER}" != "${EXPECTED_VERSION}" ]; then
                echo "[ERROR] VersionName no coincide." >&2
                echo "  Esperado: ${EXPECTED_VERSION}" >&2
                echo "  Actual:   ${ACTUAL_VER}" >&2
                exit 1
            fi
            echo "  [OK] VersionName correcto: ${ACTUAL_VER}"
        fi

        # Para producción, asegurar que no sea debuggable
        if [[ "${EXPECTED_CHANNEL}" =~ ^(prod|production)$ ]]; then
            if echo "${BADGING}" | grep -q "application-debuggable"; then
                echo "[ERROR] El APK de producción contiene 'application-debuggable'! Rechazado." >&2
                exit 1
            fi
            echo "  [OK] APK no debuggable verificado."
        fi
    fi
else
    # Fallback sin aapt: verificar presencia de AndroidManifest.xml en el zip
    if ! unzip -l "${APK_FILE}" | grep -q "AndroidManifest.xml"; then
        echo "[ERROR] AndroidManifest.xml no encontrado dentro del APK." >&2
        exit 1
    fi

    # Verificar que el package esperado aparezca en el pool de cadenas del manifest binario
    python3 -c "
import zipfile, sys

apk_path = sys.argv[1]
expected_pkg = sys.argv[2]

with zipfile.ZipFile(apk_path, 'r') as z:
    manifest_bytes = z.read('AndroidManifest.xml')
    if expected_pkg.encode('utf-8') not in manifest_bytes and expected_pkg.encode('utf-16le') not in manifest_bytes:
        print(f'[ERROR] Package {expected_pkg} no encontrado en AndroidManifest.xml', file=sys.stderr)
        sys.exit(1)
" "${APK_FILE}" "${EXPECTED_PACKAGE}"
    echo "  [OK] Package verificado en AndroidManifest.xml (fallback sin aapt)."
fi

echo "[OK] Verificación de APK completada exitosamente: ${APK_FILE}"
