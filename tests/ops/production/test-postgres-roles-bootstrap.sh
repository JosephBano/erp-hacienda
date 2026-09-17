#!/usr/bin/env bash
# Contract test for the production PostgreSQL least-privilege bootstrap.
# It intentionally uses only static inspection: no database or credentials are needed.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
readonly BOOTSTRAP="$ROOT/ops/production/10-bootstrap-postgres-roles.sql"
readonly COMPOSE="$ROOT/compose.production.yml"

fail() {
  printf 'FAIL: %s\n' "$1" >&2
  exit 1
}

[[ -f "$BOOTSTRAP" ]] || fail "missing PostgreSQL roles bootstrap"

for variable in \
  PRODUCTION_POSTGRES_OWNER_USER \
  PRODUCTION_POSTGRES_RUNTIME_USER \
  PRODUCTION_POSTGRES_RUNTIME_PASSWORD \
  PRODUCTION_POSTGRES_BACKUP_USER \
  PRODUCTION_POSTGRES_BACKUP_PASSWORD; do
  grep -Fq "$variable" "$BOOTSTRAP" \
    || fail "bootstrap must obtain $variable from its protected environment"
done

grep -Fq 'NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS' "$BOOTSTRAP" \
  || fail 'runtime and backup roles must have no elevated PostgreSQL attributes'
grep -Fq 'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA' "$BOOTSTRAP" \
  || fail 'runtime role must receive only DML table privileges'
grep -Fq 'GRANT SELECT ON ALL TABLES IN SCHEMA' "$BOOTSTRAP" \
  || fail 'backup role must be read-only'
grep -Fq 'ALTER DEFAULT PRIVILEGES FOR ROLE' "$BOOTSTRAP" \
  || fail 'future migration tables must preserve least privilege'
grep -Fq '10-bootstrap-postgres-roles.sql:/docker-entrypoint-initdb.d/10-bootstrap-postgres-roles.sql:ro' "$COMPOSE" \
  || fail 'compose must mount the versioned bootstrap read-only'

if grep -Eq 'PASSWORD[[:space:]]+['\''"][^:'\''"]' "$BOOTSTRAP"; then
  fail 'bootstrap contains a literal password'
fi

printf 'PASS: PostgreSQL roles bootstrap contract\n'
