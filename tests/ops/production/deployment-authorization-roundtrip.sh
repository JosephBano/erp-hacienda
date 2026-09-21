#!/usr/bin/env bash
# Round-trip: firmar como el workflow y verificar como el servidor.
#
# El bug que cubre: el workflow firmaba el archivo tal cual lo escribia `jq -n`
# (pretty-printed, claves en orden de escritura, con salto final) mientras que
# deploy-entry reconstruye el payload con `jq -cS '{release, sha, run_id,
# run_attempt, digests}'` (compacto, claves ordenadas, sin salto) y verifica ESO.
# Tres diferencias de bytes a la vez, asi que la firma no podia verificar nunca
# aunque la clave fuera la correcta. El despliegue murio ahi con "la firma de
# autorizacion no es valida" (run 35566153050).
#
# Una firma SSH cubre bytes exactos, de modo que las dos recetas tienen que
# producir la MISMA secuencia. Esta prueba las ejecuta de verdad y firma y
# verifica de punta a punta. Sin red, sin secretos, sin root.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
entry="$repo_root/scripts/production-deploy-entry.sh"
workflow="$repo_root/.github/workflows/deploy-production.yml"

fail() { echo "FAIL: $1" >&2; exit 1; }

command -v jq >/dev/null 2>&1 || { echo "SKIP: jq no disponible"; exit 0; }
command -v ssh-keygen >/dev/null 2>&1 || { echo "SKIP: ssh-keygen no disponible"; exit 0; }

# Las dos recetas deben seguir declaradas tal cual en sus archivos: si alguien
# cambia una sin la otra, esta prueba deja de proteger nada.
grep -Fq "jq -cS '{release, sha, run_id, run_attempt, digests}'" "$entry" \
    || fail 'deploy-entry must canonicalise the payload with jq -cS before verifying'
grep -Fq "jq -cS '{release, sha, run_id, run_attempt, digests}'" "$workflow" \
    || fail 'workflow must sign the same canonical jq -cS payload the server verifies'

w="$(mktemp -d)"; trap 'rm -rf "$w"' EXIT
ssh-keygen -q -t ed25519 -N "" -C roundtrip -f "$w/key"

release="v1.2.3"
sha="$(printf '0%.0s' $(seq 40))"
run_id="35566153050"; run_attempt="1"
api_image="ghcr.io/josephbano/hato-api"; api_digest="sha256:$(printf 'a%.0s' $(seq 64))"
migrate_image="ghcr.io/josephbano/hato-migrate"; migrate_digest="sha256:$(printf 'b%.0s' $(seq 64))"

# --- Lado workflow: construir el payload canonico y firmarlo ---------------
jq -n --arg release "$release" --arg sha "$sha" --arg run_id "$run_id" \
    --arg run_attempt "$run_attempt" --arg api_image "$api_image" \
    --arg api_digest "$api_digest" --arg migrate_image "$migrate_image" \
    --arg migrate_digest "$migrate_digest" \
    '{release: $release, sha: $sha, run_id: $run_id, run_attempt: $run_attempt, digests: {($api_image): $api_digest, ($migrate_image): $migrate_digest}}' \
    | jq -cS '{release, sha, run_id, run_attempt, digests}' > "$w/canonical"
printf '%s' "$(cat "$w/canonical")" > "$w/payload.json"

ssh-keygen -Y sign -f "$w/key" -n hato-production "$w/payload.json" >/dev/null 2>&1 \
    || fail 'no se pudo firmar el payload de prueba'

authorization="$(base64 -w 0 "$w/payload.json.sig")"
jq --arg authorization "$authorization" '. + {authorization: $authorization}' \
    "$w/payload.json" > "$w/manifest.json"

# --- Lado servidor: reconstruir el payload desde el manifiesto recibido ----
request_raw="$(cat "$w/manifest.json")"
server_payload="$(printf '%s' "$request_raw" | jq -cS '{release, sha, run_id, run_attempt, digests}')"

signed_bytes="$(cat "$w/payload.json")"
[ "$server_payload" = "$signed_bytes" ] \
    || fail "los bytes firmados y los verificados no coinciden:
  firmado:   $signed_bytes
  verificado: $server_payload"

printf '%s' "$authorization" | base64 -d > "$w/sig" 2>/dev/null \
    || fail 'authorization no es base64 valido'
printf '%s ' "hato-production" > "$w/allowed-signers"
cat "$w/key.pub" >> "$w/allowed-signers"

printf '%s' "$server_payload" | ssh-keygen -Y verify \
    -f "$w/allowed-signers" -I hato-production -n hato-production -s "$w/sig" >/dev/null 2>&1 \
    || fail 'la firma no verifica con la receta del servidor'

# Y alterar el manifiesto debe romper la verificacion.
jq '.sha = "'"$(printf 'f%.0s' $(seq 40))"'"' "$w/manifest.json" > "$w/tampered.json"
tampered_payload="$(jq -cS '{release, sha, run_id, run_attempt, digests}' "$w/tampered.json")"
if printf '%s' "$tampered_payload" | ssh-keygen -Y verify \
    -f "$w/allowed-signers" -I hato-production -n hato-production -s "$w/sig" >/dev/null 2>&1; then
    fail 'un manifiesto alterado paso la verificacion'
fi

echo 'PASS: authorization sign/verify round-trip'
