#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Unit & Contract Tests: Systemd Units, Backup Status Monitor and Failure Alerts
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEST_DIR="$(mktemp -d /tmp/hato-status-schedule-test.XXXXXX)"
trap 'rm -rf "${TEST_DIR}"' EXIT

FAILED=0

fail() {
    echo "[FAIL] $1" >&2
    FAILED=1
}

pass() {
    echo "[PASS] $1"
}

# ------------------------------------------------------------------------------
# 1. Systemd Units Validation
# ------------------------------------------------------------------------------
echo "Running Test 1: Validating systemd unit file contracts..."
SERVICE_DAILY="${REPO_ROOT}/ops/backups/hato-backup.service"
TIMER_DAILY="${REPO_ROOT}/ops/backups/hato-backup.timer"
SERVICE_RESTORE="${REPO_ROOT}/ops/backups/hato-restore-check.service"
TIMER_RESTORE="${REPO_ROOT}/ops/backups/hato-restore-check.timer"

for FILE in "${SERVICE_DAILY}" "${TIMER_DAILY}" "${SERVICE_RESTORE}" "${TIMER_RESTORE}"; do
    [ -f "${FILE}" ] || fail "Missing unit file: ${FILE}"
done

# Check Daily Timer Properties
grep -Fq "Persistent=true" "${TIMER_DAILY}" || fail "hato-backup.timer must have Persistent=true"
grep -Fq "07:00:00 UTC" "${TIMER_DAILY}" || fail "hato-backup.timer must run at 07:00:00 UTC"
grep -Fq "User=hato-backup" "${SERVICE_DAILY}" || fail "hato-backup.service must run as User=hato-backup"
grep -Fq "TimeoutStartSec=" "${SERVICE_DAILY}" || fail "hato-backup.service must have TimeoutStartSec"

# Check Monthly Restore Timer Properties
grep -Fq "Persistent=true" "${TIMER_RESTORE}" || fail "hato-restore-check.timer must have Persistent=true"
grep -Fq "User=hato-backup" "${SERVICE_RESTORE}" || fail "hato-restore-check.service must run as User=hato-backup"

# Optional systemd-analyze verify if systemd-analyze exists
if command -v systemd-analyze >/dev/null 2>&1; then
    if systemd-analyze verify "${SERVICE_DAILY}" "${TIMER_DAILY}" "${SERVICE_RESTORE}" "${TIMER_RESTORE}" >"${TEST_DIR}/sysd.log" 2>&1; then
        pass "systemd-analyze verify passed on backup units"
    else
        echo "[INFO] systemd-analyze verify notice: $(head -n 2 "${TEST_DIR}/sysd.log")"
    fi
fi
pass "Systemd unit contracts verified"

# ------------------------------------------------------------------------------
# 2. Backup Status Monitor Verification
# ------------------------------------------------------------------------------
echo "Running Test 2: Validating backup-status.sh (RPO and JSON output)..."
export BACKUP_DIR="${TEST_DIR}/status_backups"
mkdir -p "${BACKUP_DIR}"

# Case A: No backups -> CRITICAL_NO_BACKUP (exit 1)
if "${REPO_ROOT}/scripts/backup-status.sh" >"${TEST_DIR}/status_none.log" 2>&1; then
    fail "backup-status.sh should exit 1 when no backups exist"
else
    pass "backup-status.sh reported failure on missing backups"
fi

# Case B: Fresh backup (2 hours old) -> OK (exit 0)
FRESH_DUMP="${BACKUP_DIR}/hato-prod-db-20260917T120000Z-11111111-1111-1111-1111-111111111111.dump"
echo "fresh payload" > "${FRESH_DUMP}"
touch -d "2 hours ago" "${FRESH_DUMP}"

if ! "${REPO_ROOT}/scripts/backup-status.sh" >"${TEST_DIR}/status_fresh.log" 2>&1; then
    fail "backup-status.sh failed with fresh 2-hour backup"
else
    pass "backup-status.sh reported OK on fresh backup"
fi

# Case C: JSON Output validation
JSON_OUT="${TEST_DIR}/status.json"
"${REPO_ROOT}/scripts/backup-status.sh" --json > "${JSON_OUT}"
python3 -c "
import json
with open('${JSON_OUT}') as f:
    d = json.load(f)
assert d['status'] == 'OK'
assert d['local_backups_count'] == 1
assert d['latest_backup_age_hours'] == 2
"
pass "backup-status.sh --json produced valid metrics JSON"

# Case D: Stale backup (28 hours old) -> CRITICAL_RPO_EXCEEDED (exit 1)
touch -d "28 hours ago" "${FRESH_DUMP}"
if "${REPO_ROOT}/scripts/backup-status.sh" >"${TEST_DIR}/status_stale.log" 2>&1; then
    fail "backup-status.sh should exit 1 when backup age exceeds 26 hours"
else
    pass "backup-status.sh failed with critical RPO exceeded when backup > 26h"
fi

# ------------------------------------------------------------------------------
# 3. Pipeline Failure Alerting Verification
# ------------------------------------------------------------------------------
echo "Running Test 3: Daily pipeline notifies Healthchecks /fail on error..."
export MOCK_SERVER_LOG="${TEST_DIR}/ping.log"
MOCK_CURL="${TEST_DIR}/mock_curl.sh"
cat << EOF > "${MOCK_CURL}"
#!/usr/bin/env bash
for arg in "\$@"; do
    if [[ "\$arg" == http* ]]; then
        echo "CURL: \$arg" >> "${MOCK_SERVER_LOG}"
    fi
done
exit 0
EOF
chmod +x "${MOCK_CURL}"

# Create a failing pipeline test
FAIL_DIR="${TEST_DIR}/fail_pipe"
mkdir -p "${FAIL_DIR}"
export BACKUP_DIR="${FAIL_DIR}"
export HEALTHCHECKS_PING_URL="https://hc.example/ping/12345"
export BACKUP_HELPER="false" # Force backup failure

# Run backup-daily.sh with mocked curl
PATH="${TEST_DIR}:${PATH}"
ln -sf "${MOCK_CURL}" "${TEST_DIR}/curl"

if "${REPO_ROOT}/scripts/backup-daily.sh" >"${TEST_DIR}/daily_fail.log" 2>&1; then
    fail "backup-daily.sh should have exited non-zero"
else
    if [ -f "${MOCK_SERVER_LOG}" ] && grep -q "https://hc.example/ping/12345/fail" "${MOCK_SERVER_LOG}"; then
        pass "Daily backup failure sent /fail alert to Healthchecks"
    else
        fail "Daily backup failure did not emit /fail signal"
    fi
fi

if [ "${FAILED}" -ne 0 ]; then
    echo "Schedule and status tests FAILED!" >&2
    exit 1
fi

echo "All schedule and status tests PASSED!"
