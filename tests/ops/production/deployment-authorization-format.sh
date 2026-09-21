#!/usr/bin/env bash
# Regression test for the authorization format check in production-deploy-entry.sh.
#
# El bug que cubre: la comprobación usaba `[[ =~ ^[A-Za-z0-9+/=]{32,65536}$ ]]`.
# Ese intervalo supera el RE_DUP_MAX de glibc (32767), así que bash no llegaba a
# compilar la expresión y abortaba con "Regular expression too big". El `[[ =~ ]]`
# devolvía código de error, el `!` lo tomaba como verdadero y TODA autorización
# legítima se rechazaba. El primer despliegue a producción murió justo ahí
# (run 35562094501: "[deploy-entry] rechazado: authorization con formato inválido").
#
# Esta prueba firma un manifiesto de verdad con ssh-keygen y comprueba que
# deploy-entry ya no lo rechaza por formato. No necesita red, ni secretos, ni root:
# la validación de formato ocurre ANTES de la de firmantes autorizados, así que en
# una máquina de desarrollo el script debe avanzar hasta ese punto posterior.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
entry="$repo_root/scripts/production-deploy-entry.sh"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

command -v jq >/dev/null 2>&1 || { echo "SKIP: jq no disponible"; exit 0; }
command -v ssh-keygen >/dev/null 2>&1 || { echo "SKIP: ssh-keygen no disponible"; exit 0; }

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT

ssh-keygen -q -t ed25519 -N "" -C authorization-format-test -f "$workdir/key"

# Manifiesto con todos los campos en el formato que deploy-entry exige.
sha_hex="$(printf '0%.0s' $(seq 40))"
digest_api="sha256:$(printf 'a%.0s' $(seq 64))"
digest_migrate="sha256:$(printf 'b%.0s' $(seq 64))"

jq -n \
    --arg release "v1.0.0" \
    --arg sha "$sha_hex" \
    --arg run_id "35562094501" \
    --arg run_attempt "1" \
    --arg digest_api "$digest_api" \
    --arg digest_migrate "$digest_migrate" \
    '{release: $release, sha: $sha, run_id: $run_id, run_attempt: $run_attempt,
      digests: {
        "ghcr.io/josephbano/hato-api": $digest_api,
        "ghcr.io/josephbano/hato-migrate": $digest_migrate
      }}' > "$workdir/payload.json"

ssh-keygen -Y sign -f "$workdir/key" -n hato-production "$workdir/payload.json" >/dev/null 2>&1 \
    || fail "no se pudo firmar el manifiesto de prueba"

authorization="$(base64 -w 0 "$workdir/payload.json.sig")"
[ "${#authorization}" -ge 32 ] || fail "la firma de prueba salió demasiado corta"

jq --arg authorization "$authorization" '. + {authorization: $authorization}' \
    "$workdir/payload.json" > "$workdir/manifest.json"

# deploy-entry fallará más adelante (no hay allowed-signers root-owned en una
# máquina de desarrollo). Lo que esta prueba exige es que NO falle por formato.
stderr_output="$(bash "$entry" < "$workdir/manifest.json" 2>&1 >/dev/null || true)"

case "$stderr_output" in
    *"authorization con formato inválido"*)
        fail "una autorización base64 legítima fue rechazada por formato: $stderr_output"
        ;;
    *"Regular expression too big"*)
        fail "la validación sigue usando un cuantificador que bash no puede compilar"
        ;;
esac

# Y una autorización que de verdad está mal debe seguir rechazándose.
jq '. + {authorization: "no-es-base64 ***"}' "$workdir/payload.json" > "$workdir/bad.json"
bad_output="$(bash "$entry" < "$workdir/bad.json" 2>&1 >/dev/null || true)"
case "$bad_output" in
    *"authorization con formato inválido"*) ;;
    *) fail "una autorización inválida NO fue rechazada por formato: $bad_output" ;;
esac

echo 'PASS: authorization format accepts real signatures and rejects malformed input'
