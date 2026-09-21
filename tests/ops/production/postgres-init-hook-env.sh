#!/usr/bin/env bash
# Toda variable que el hook de roles lee con \getenv tiene que existir DENTRO del
# contenedor de postgres.
#
# El bug que cubre: compose.production.yml metia el owner solo como POSTGRES_USER,
# renombrandolo, mientras que 10-bootstrap-postgres-roles.sql hace
# `\getenv owner_user PRODUCTION_POSTGRES_OWNER_USER`. Esa variable no existia en
# el contenedor, \getenv dejo la variable de psql sin definir, `:'owner_user'` se
# quedo literal y el hook murio con "syntax error at or near \":\"". Postgres
# arranco unhealthy, el despliegue aborto, y los roles runtime/backup no se
# crearon (run 35567780579). Las otras cuatro variables si pasaban con su nombre
# original, que es justo por que solo fallo esa.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
hook="$repo_root/ops/production/10-bootstrap-postgres-roles.sql"
compose="$repo_root/compose.production.yml"

fail() { echo "FAIL: $1" >&2; exit 1; }

[ -f "$hook" ] || fail "no se encontro el hook de roles"
[ -f "$compose" ] || fail "no se encontro compose.production.yml"

# El bloque environment del servicio postgres: desde su 'environment:' hasta la
# siguiente clave al mismo nivel de indentacion.
postgres_env="$(awk '
    /^  postgres:/        { in_svc = 1; next }
    in_svc && /^  [a-z]/  { in_svc = 0 }
    in_svc && /^    environment:/ { in_env = 1; next }
    in_env && /^    [a-z]/ { in_env = 0 }
    in_env && in_svc      { print }
' "$compose")"

[ -n "$postgres_env" ] || fail 'no se pudo extraer el bloque environment del servicio postgres'

missing=0
while read -r var; do
    [ -z "$var" ] && continue
    if ! printf '%s\n' "$postgres_env" | grep -qE "^[[:space:]]*${var}:"; then
        echo "FAIL: el hook lee '$var' con \\getenv pero compose.production.yml no la define en el contenedor de postgres" >&2
        missing=1
    fi
done <<< "$(grep -oE '^\\getenv[[:space:]]+[a-z_]+[[:space:]]+[A-Z_]+' "$hook" | awk '{print $3}')"

[ "$missing" -eq 0 ] || fail 'faltan variables de entorno para el hook de inicializacion'

echo 'PASS: postgres init hook env vars are all provided by compose'
