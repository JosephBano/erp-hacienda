#!/usr/bin/env bash
set -euo pipefail

# Regression test for feature-0013: the scheduled restore must never use
# Oracle's production PostgreSQL container or fall back to a local dump.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEST_DIR="$(mktemp -d /tmp/hato-restore-isolation-test.XXXXXX)"
trap 'rm -rf "${TEST_DIR}"' EXIT

fail() {
    echo "[FAIL] $1" >&2
    exit 1
}

pass() {
    echo "[PASS] $1"
}

RESTORE_SCRIPT="${REPO_ROOT}/scripts/backup-restore-check.sh"
RESTORE_SERVICE="${REPO_ROOT}/ops/backups/hato-restore-check.service"
RESTORE_COMPOSE="${REPO_ROOT}/ops/backups/hato-restore-check.compose.yml"
RESTORE_ENV_EXAMPLE="${REPO_ROOT}/ops/backups/restore.env.example"

for required_file in "${RESTORE_SCRIPT}" "${RESTORE_SERVICE}" "${RESTORE_COMPOSE}" "${RESTORE_ENV_EXAMPLE}"; do
    [ -f "${required_file}" ] || fail "Missing isolated restore artifact: ${required_file}"
done

grep -Fq 'POSTGRES_CONTAINER=hato-production-postgres' "${REPO_ROOT}/ops/backups/backup.env.example" \
    || fail "Oracle backup configuration must target hato-production-postgres"
grep -Fq 'BACKUP_LOCK_FILE=/var/backups/hato-db/hato-backup.lock' "${REPO_ROOT}/ops/backups/backup.env.example" \
    || fail "Backup lock must be inside the writable backup directory"
pass "Oracle backup defaults are production-safe"

grep -Fq 'User=root' "${RESTORE_SERVICE}" \
    || fail "Isolated restore service must run under the home-server root-owned boundary"
grep -Fq 'EnvironmentFile=-/etc/hato-restore/restore.env' "${RESTORE_SERVICE}" \
    || fail "Isolated restore service must read only its home-server configuration"
grep -Fq 'internal: true' "${RESTORE_COMPOSE}" \
    || fail "Restore PostgreSQL network must be internal"
grep -Fq 'POSTGRES_CONTAINER=hato-restore-check-postgres' "${RESTORE_ENV_EXAMPLE}" \
    || fail "Restore configuration must use its dedicated PostgreSQL container"
pass "Home-server restore artifacts declare an isolated boundary"

set +e
RESTORE_POSTGRES_CONTAINER=hato-restore-check-postgres \
RESTORE_COMPOSE_FILE="${RESTORE_COMPOSE}" \
RESTORE_POSTGRES_PASSWORD=test-only \
HATO_RESTORE_ENV_FILE="${TEST_DIR}/missing-restore.env" \
"${RESTORE_SCRIPT}" >"${TEST_DIR}/missing-config.log" 2>&1
exit_code=$?
set -e

[ "${exit_code}" -ne 0 ] || fail "Restore check accepted a missing home-server configuration"
grep -Fq 'No existe la configuración exclusiva del home-server' "${TEST_DIR}/missing-config.log" \
    || fail "Restore check did not reject a missing home-server configuration"
pass "Restore check requires its home-server-only configuration"

set +e
RESTORE_POSTGRES_CONTAINER=hato-production-postgres \
RCLONE_CONFIG="${TEST_DIR}/missing-rclone.conf" \
"${RESTORE_SCRIPT}" >"${TEST_DIR}/reject-production.log" 2>&1
exit_code=$?
set -e

[ "${exit_code}" -ne 0 ] || fail "Restore check accepted a production PostgreSQL container"
grep -Fq 'contenedor aislado' "${TEST_DIR}/reject-production.log" \
    || fail "Restore check did not report its isolated-container guard"
pass "Restore check rejects a production container before accessing Drive"

if rg -F 'Fallback to local backup dir' "${RESTORE_SCRIPT}" >/dev/null; then
    fail "Scheduled restore must not fall back to a local Oracle backup"
fi
pass "Restore check requires a remote completed backup"
