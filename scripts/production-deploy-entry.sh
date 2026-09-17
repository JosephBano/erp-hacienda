#!/usr/bin/env bash
# deploy-entry — comando forzado SSH para la cuenta sin privilegios hato-deploy.
#
# Se instala como /usr/local/libexec/hato/deploy-entry (root:root, 755, no escribible
# por hato-deploy). sshd lo invoca vía la entrada `command=` de authorized_keys y el
# ForceCommand de ops/production/sshd-hato.conf, sin importar qué pida el cliente SSH.
#
# Contrato: lee UNA petición JSON pequeña por stdin con exactamente los campos
# release, sha y digests. NO usa $SSH_ORIGINAL_COMMAND como código en ningún momento
# (el spec lo prohíbe explícitamente) — esta variable nunca se referencia aquí.
#
# Si la petición pasa el allowlist estricto, invoca el helper root-owned
# /usr/local/libexec/hato/deploy-root vía sudo, pasándole la petición ya validada
# y re-serializada por stdin. Si cualquier validación falla: sale con error, sin
# ejecutar nada y sin imprimir secretos.
#
# Ver ops/production/bootstrap.md para el procedimiento de instalación completo.

set -euo pipefail

readonly DEPLOY_ROOT_HELPER="/usr/local/libexec/hato/deploy-root"
readonly AUTHORIZATION_ALLOWED_SIGNERS="/etc/hato-production/deploy-authorization.allowed-signers"
readonly MAX_REQUEST_BYTES=65536

# Allowlist estricta de imágenes publicadas por el repositorio de producción.
# Debe mantenerse sincronizada con el owner que resuelve el workflow de despliegue.
readonly ALLOWED_IMAGES=(
    "ghcr.io/josephbano/hato-api"
    "ghcr.io/josephbano/hato-migrate"
)

fail() {
    # Nunca imprimir el contenido de la petición recibida: puede incluir
    # entradas maliciosas que no queremos reflejar en logs.
    echo "[deploy-entry] rechazado: $1" >&2
    exit 1
}

command -v jq >/dev/null 2>&1 || fail "jq no está disponible en este host"

# Limitar el tamaño de la entrada antes de tocarla con jq, para evitar que una
# petición enorme se use para agotar memoria o abusar del parser.
request_raw="$(head -c "$((MAX_REQUEST_BYTES + 1))")"
if [ "${#request_raw}" -gt "$MAX_REQUEST_BYTES" ]; then
    fail "la petición excede el tamaño máximo permitido"
fi
[ -n "$request_raw" ] || fail "petición vacía"

# JSON bien formado.
if ! printf '%s' "$request_raw" | jq empty >/dev/null 2>&1; then
    fail "la petición no es JSON válido"
fi

# Exactamente los campos esperados, sin campos extra desconocidos.
allowed_keys='["authorization","digests","release","run_attempt","run_id","sha"]'
actual_keys="$(printf '%s' "$request_raw" | jq -c '(keys | sort)')"
expected_keys="$(printf '%s' "$allowed_keys" | jq -c 'sort')"
if [ "$actual_keys" != "$expected_keys" ]; then
    fail "la petición no tiene exactamente los campos release, sha, run_id, run_attempt, digests y authorization"
fi

# release: tag inmutable, formato conservador (letras, números, punto, guion, guion bajo).
release="$(printf '%s' "$request_raw" | jq -r '.release // empty')"
if ! [[ "$release" =~ ^[A-Za-z0-9._-]{1,128}$ ]]; then
    fail "release con formato inválido"
fi

# sha: commit SHA hexadecimal completo (40 caracteres).
sha="$(printf '%s' "$request_raw" | jq -r '.sha // empty')"
if ! [[ "$sha" =~ ^[0-9a-f]{40}$ ]]; then
    fail "sha con formato inválido"
fi

# El run y su intento hacen que una autorización sea específica a una ejecución de
# GitHub Actions. Ambos se firman con el resto del manifiesto y deploy-root conserva
# el par consumido antes de mutar el stack.
run_id="$(printf '%s' "$request_raw" | jq -r '.run_id // empty')"
run_attempt="$(printf '%s' "$request_raw" | jq -r '.run_attempt // empty')"
if ! [[ "$run_id" =~ ^[1-9][0-9]{0,19}$ ]] || ! [[ "$run_attempt" =~ ^[1-9][0-9]{0,9}$ ]]; then
    fail "run_id o run_attempt con formato inválido"
fi

# digests: objeto no vacío, imagen -> digesto sha256, solo imágenes de la allowlist.
digests_type="$(printf '%s' "$request_raw" | jq -r '.digests | type')"
[ "$digests_type" = "object" ] || fail "digests debe ser un objeto"

digest_count="$(printf '%s' "$request_raw" | jq -r '.digests | length')"
[ "$digest_count" -gt 0 ] || fail "digests no puede estar vacío"

is_allowed_image() {
    local candidate="$1"
    local allowed
    for allowed in "${ALLOWED_IMAGES[@]}"; do
        if [ "$candidate" = "$allowed" ]; then
            return 0
        fi
    done
    return 1
}

while IFS=$'\t' read -r image digest; do
    is_allowed_image "$image" || fail "imagen fuera de la allowlist"
    # Digesto sha256 estricto: nada que parezca comando, ruta o flag.
    if ! [[ "$digest" =~ ^sha256:[0-9a-f]{64}$ ]]; then
        fail "digesto con formato inválido para $image"
    fi
done < <(printf '%s' "$request_raw" | jq -r '.digests | to_entries[] | [.key, .value] | @tsv')

authorization="$(printf '%s' "$request_raw" | jq -r '.authorization // empty')"
if ! [[ "$authorization" =~ ^[A-Za-z0-9+/=]{32,65536}$ ]]; then
    fail "authorization con formato inválido"
fi

[ -f "$AUTHORIZATION_ALLOWED_SIGNERS" ] && [ ! -L "$AUTHORIZATION_ALLOWED_SIGNERS" ] \
    || fail "no existe el archivo root-owned de firmantes autorizados"
[ "$(stat -c '%a:%U:%G' "$AUTHORIZATION_ALLOWED_SIGNERS")" = "644:root:root" ] \
    || fail "el archivo de firmantes autorizados debe ser root:root con modo 644"

# GitHub Environment entrega DEPLOY_AUTHORIZATION_KEY solo al job aprobado. El host
# verifica su firma OpenSSH sobre un JSON canónico, por lo que ni CI SSH ni un payload
# reinyectado pueden cambiar run, SHA o artefactos sin la clave independiente.
authorization_payload="$(printf '%s' "$request_raw" | jq -cS '{release, sha, run_id, run_attempt, digests}')"
authorization_signature="$(mktemp)"
trap 'rm -f "$authorization_signature"' EXIT
printf '%s' "$authorization" | base64 --decode > "$authorization_signature" 2>/dev/null \
    || fail "authorization no es base64 válido"
if ! printf '%s' "$authorization_payload" | ssh-keygen -Y verify \
    -f "$AUTHORIZATION_ALLOWED_SIGNERS" \
    -I hato-production \
    -n hato-production \
    -s "$authorization_signature" >/dev/null 2>&1; then
    fail "la firma de autorización no es válida"
fi

# Re-serializar la petición ya validada a partir de los valores extraídos (no
# reenviamos el blob original tal cual): esto garantiza que deploy-root solo ve
# datos que ya pasaron por esta validación campo por campo.
validated_request="$(
    jq -n \
        --arg release "$release" \
        --arg sha "$sha" \
        --arg run_id "$run_id" \
        --arg run_attempt "$run_attempt" \
        --argjson digests "$(printf '%s' "$request_raw" | jq -c '.digests')" \
        '{release: $release, sha: $sha, run_id: $run_id, run_attempt: $run_attempt, digests: $digests}'
)"

# Nunca por SSH_ORIGINAL_COMMAND, nunca por argumento de proceso (grep-eable en ps):
# se pasa por stdin al helper root-owned invocado con ruta absoluta exacta.
exec sudo "$DEPLOY_ROOT_HELPER" <<< "$validated_request"
