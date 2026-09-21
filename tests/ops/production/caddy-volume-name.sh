#!/usr/bin/env bash
# El volumen de Caddy debe llamarse igual en compose.production.yml y en
# scripts/production-tls-renew.sh.
#
# El bug que cubre: Compose antepone el nombre del proyecto a los volumenes
# declarados, asi que "hato-production-caddy-data" se creaba y montaba como
# "hato-production_hato-production-caddy-data". production-tls-renew.sh escribe
# el certificado en el nombre SIN prefijo, de modo que el certificado quedaba
# sano en un volumen y Caddy montaba otro vacio, muriendo en bucle con
# "open /data/tls/server.crt: no such file or directory" (run 35569428731).
# `name:` explicito desactiva el prefijado.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
compose="$repo_root/compose.production.yml"
renew="$repo_root/scripts/production-tls-renew.sh"

fail() { echo "FAIL: $1" >&2; exit 1; }

command -v python3 >/dev/null 2>&1 || { echo "SKIP: python3 no disponible"; exit 0; }

# Nombre efectivo del volumen segun compose: el `name:` explicito si existe.
compose_volume="$(python3 - "$compose" <<'PY'
import sys, yaml
d = yaml.safe_load(open(sys.argv[1]))
v = (d.get("volumes") or {}).get("hato-production-caddy-data")
print((v or {}).get("name", "") if isinstance(v, dict) else "")
PY
)"

[ -n "$compose_volume" ] \
    || fail 'hato-production-caddy-data debe declarar `name:` explicito, o Compose le antepone el nombre del proyecto y Caddy monta un volumen vacio'

# Nombre que usa el script de renovacion (su valor por defecto).
renew_volume="$(grep -oE 'HATO_CADDY_VOLUME:-[a-zA-Z0-9_-]+' "$renew" | head -1 | cut -d- -f2-)"
renew_volume="${renew_volume#:-}"
renew_volume="$(grep -oE 'HATO_CADDY_VOLUME:-[a-zA-Z0-9_-]+\}' "$renew" | head -1 | sed 's/.*:-//; s/}//')"

[ -n "$renew_volume" ] || fail 'no se pudo leer HATO_CADDY_VOLUME de production-tls-renew.sh'

[ "$compose_volume" = "$renew_volume" ] \
    || fail "el volumen de Caddy no coincide:
  compose monta:      $compose_volume
  tls-renew escribe:  $renew_volume"

echo "PASS: caddy volume name matches ($compose_volume)"
