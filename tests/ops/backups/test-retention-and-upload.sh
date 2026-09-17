#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Unit & Contract Tests: Backup Upload, Retention, Quotas and Safe Pruning
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEST_DIR="$(mktemp -d /tmp/hato-upload-retention-test.XXXXXX)"
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
# 1. Inspect example configuration files for secrets
# ------------------------------------------------------------------------------
echo "Running Test 1: Validate configuration examples have no real secrets..."
RCLONE_EXAMPLE="${REPO_ROOT}/ops/backups/rclone.conf.example"
ENV_EXAMPLE="${REPO_ROOT}/ops/backups/backup.env.example"

[ -f "${RCLONE_EXAMPLE}" ] || fail "Missing ops/backups/rclone.conf.example"
[ -f "${ENV_EXAMPLE}" ] || fail "Missing ops/backups/backup.env.example"

if grep -Eq '([a-zA-Z0-9_\-\.]+@[a-zA-Z0-9_\-\.]+\.[a-zA-Z]{2,5})' "${RCLONE_EXAMPLE}" "${ENV_EXAMPLE}"; then
    fail "Example configurations contain a real email address"
else
    pass "No email addresses in example configuration files"
fi

if grep -Eq 'password[[:space:]]*=[[:space:]]*[a-zA-Z0-9_\-]{16,}' "${RCLONE_EXAMPLE}"; then
    fail "rclone.conf.example appears to contain an unmasked literal password"
fi
pass "Configuration examples verified safe"

# ------------------------------------------------------------------------------
# 2. Upload script: Shared Drive 300 GB limit and threshold alerts
# ------------------------------------------------------------------------------
echo "Running Test 2: Upload script enforces 300 GB limit and emits threshold warnings..."
MOCK_RCLONE="${TEST_DIR}/mock_rclone.sh"
cat << 'EOF' > "${MOCK_RCLONE}"
#!/usr/bin/env bash
# Mock rclone for quota and copy operations
CMD="$1"
shift
if [ "$CMD" = "size" ]; then
    # Return simulated bytes from MOCK_REMOTE_BYTES env
    BYTES="${MOCK_REMOTE_BYTES:-0}"
    echo "Total objects: 10"
    echo "Total size: ${BYTES} Byte (${BYTES} B)"
elif [ "$CMD" = "about" ]; then
    echo "Total: 500000000000"
    echo "Used: 100000000000"
    echo "Free: ${MOCK_ACCOUNT_FREE_BYTES:-400000000000}"
elif [ "$CMD" = "copyto" ]; then
    SRC="$1"
    DEST="$2"
    mkdir -p "$(dirname "$DEST")"
    cp "$SRC" "$DEST"
elif [ "$CMD" = "lsf" ]; then
    TARGET="$1"
    if [ -f "$TARGET" ]; then
        basename "$TARGET"
    elif [ -d "$TARGET" ]; then
        ls -1 "$TARGET" 2>/dev/null || true
    fi
elif [ "$CMD" = "cryptcheck" ] || [ "$CMD" = "check" ]; then
    exit 0
else
    exit 0
fi
EOF
chmod +x "${MOCK_RCLONE}"

# Prepare a dummy dump with manifest
DUMP_DIR="${TEST_DIR}/local_backups"
mkdir -p "${DUMP_DIR}"
STEM="hato-prod-db-20260916T070000Z-00000000-0000-0000-0000-000000000001"
DUMP_FILE="${DUMP_DIR}/${STEM}.dump"
echo "dummy dump payload" > "${DUMP_FILE}"
(cd "${DUMP_DIR}" && sha256sum "$(basename "${DUMP_FILE}")" > "${DUMP_FILE%.dump}.sha256")
"${REPO_ROOT}/scripts/backup-manifest.sh" generate "${DUMP_FILE}" "${DUMP_FILE%.dump}.manifest.json" "production" "hato_production" "test_sha"

# Case A: Below 210 GB -> normal upload without warning
export RCLONE_CMD="${MOCK_RCLONE}"
export MOCK_REMOTE_BYTES="100000000000" # 100 GB
export RCLONE_REMOTE="${TEST_DIR}/remote"
mkdir -p "${RCLONE_REMOTE}/database/prod/daily"

LOG_A="${TEST_DIR}/upload_normal.log"
if ! "${REPO_ROOT}/scripts/backup-upload.sh" "${DUMP_FILE}" >"${LOG_A}" 2>&1; then
    fail "Upload failed under normal quota"
else
    if [ ! -f "${RCLONE_REMOTE}/database/prod/daily/${STEM}.complete" ]; then
        fail "Complete marker was not published to remote"
    else
        pass "Normal upload succeeded and published .complete marker"
    fi
fi

# Case B: Warning threshold 210 GB (70%)
export MOCK_REMOTE_BYTES="215000000000" # 215 GB
LOG_B="${TEST_DIR}/upload_warn.log"
if ! "${REPO_ROOT}/scripts/backup-upload.sh" "${DUMP_FILE}" >"${LOG_B}" 2>&1; then
    fail "Upload failed at 215 GB"
else
    if grep -q "70 %" "${LOG_B}" || grep -q "210 GB" "${LOG_B}"; then
        pass "Warning threshold triggered at 210 GB"
    else
        fail "Warning threshold 210 GB not reported in log"
    fi
fi

# Case C: Alert threshold 255 GB (85%)
export MOCK_REMOTE_BYTES="260000000000" # 260 GB
LOG_C="${TEST_DIR}/upload_alert.log"
if ! "${REPO_ROOT}/scripts/backup-upload.sh" "${DUMP_FILE}" >"${LOG_C}" 2>&1; then
    fail "Upload failed at 260 GB"
else
    if grep -q "85 %" "${LOG_C}" || grep -q "255 GB" "${LOG_C}"; then
        pass "Alert threshold triggered at 255 GB"
    else
        fail "Alert threshold 255 GB not reported in log"
    fi
fi

# Case D: Exceeds 300 GB decimal limit -> must abort and refuse upload
export MOCK_REMOTE_BYTES="305000000000" # 305 GB
LOG_D="${TEST_DIR}/upload_exceed.log"
if "${REPO_ROOT}/scripts/backup-upload.sh" "${DUMP_FILE}" >"${LOG_D}" 2>&1; then
    fail "Upload should have failed when exceeding 300 GB limit"
else
    pass "Upload rejected when exceeding 300 GB decimal limit"
fi

# ------------------------------------------------------------------------------
# 3. Upload script: Refusal to overwrite conflicting ID
# ------------------------------------------------------------------------------
echo "Running Test 3: Upload refuses to overwrite remote file with conflicting hash..."
export MOCK_REMOTE_BYTES="50000000000"
DEST_DAILY="${RCLONE_REMOTE}/database/prod/daily"
# Corrupt the destination dump
echo "tampered remote dump" > "${DEST_DAILY}/$(basename "${DUMP_FILE}")"
LOG_CONF="${TEST_DIR}/upload_conflict.log"
if "${REPO_ROOT}/scripts/backup-upload.sh" "${DUMP_FILE}" >"${LOG_CONF}" 2>&1; then
    fail "Upload should refuse to overwrite conflicting remote file"
else
    pass "Upload refused to overwrite conflicting remote ID"
fi

# ------------------------------------------------------------------------------
# 4. Retention: Dry-run and safe pruning
# ------------------------------------------------------------------------------
echo "Running Test 4: Retention policy, dry-run and foreign files safety..."
RETENTION_DIR="${TEST_DIR}/retention_test"
mkdir -p "${RETENTION_DIR}/daily"
mkdir -p "${RETENTION_DIR}/monthly"

# Helper to create a synthetic backup set
create_backup_set() {
    local DIR="$1"
    local STEM="$2"
    local DAYS_AGO="$3"
    local CONTENT="${4:-dump_content}"

    echo "${CONTENT}" > "${DIR}/${STEM}.dump"
    (cd "${DIR}" && sha256sum "${STEM}.dump" > "${STEM}.sha256")
    "${REPO_ROOT}/scripts/backup-manifest.sh" generate "${DIR}/${STEM}.dump" "${DIR}/${STEM}.manifest.json" "production" "hato_production" "test"
    touch "${DIR}/${STEM}.complete"

    # Adjust mtime
    touch -d "${DAYS_AGO} days ago" "${DIR}/${STEM}.dump" "${DIR}/${STEM}.sha256" "${DIR}/${STEM}.manifest.json" "${DIR}/${STEM}.complete"
}

# 1. A very old backup (45 days ago) - should be pruned unless protected
create_backup_set "${RETENTION_DIR}/daily" "hato-prod-db-20260801T070000Z-11111111-1111-1111-1111-111111111111" 45
# 2. A recent backup (5 days ago) - should be kept
create_backup_set "${RETENTION_DIR}/daily" "hato-prod-db-20260912T070000Z-22222222-2222-2222-2222-222222222222" 5
# 3. A foreign file belonging to another process or user
echo "photo or document" > "${RETENTION_DIR}/daily/family_photo.jpg"
echo "apk artifact" > "${RETENTION_DIR}/daily/app-release.apk"

# Test dry-run: should not delete anything
export REMOTE_DAILY_DIR="${RETENTION_DIR}/daily"
export REMOTE_MONTHLY_DIR="${RETENTION_DIR}/monthly"

DRY_LOG="${TEST_DIR}/retention_dry.log"
"${REPO_ROOT}/scripts/backup-retention.sh" --dry-run >"${DRY_LOG}" 2>&1

[ -f "${RETENTION_DIR}/daily/hato-prod-db-20260801T070000Z-11111111-1111-1111-1111-111111111111.dump" ] || fail "Dry-run deleted old backup"
[ -f "${RETENTION_DIR}/daily/family_photo.jpg" ] || fail "Dry-run deleted foreign file"
pass "Retention dry-run reported actions without modifying files"

# Test real retention execution
REAL_LOG="${TEST_DIR}/retention_real.log"
"${REPO_ROOT}/scripts/backup-retention.sh" >"${REAL_LOG}" 2>&1

# Foreign files MUST be untouched
[ -f "${RETENTION_DIR}/daily/family_photo.jpg" ] || fail "Retention deleted foreign file family_photo.jpg!"
[ -f "${RETENTION_DIR}/daily/app-release.apk" ] || fail "Retention deleted foreign file app-release.apk!"
pass "Foreign files left completely untouched"

# Recent backup MUST be preserved
[ -f "${RETENTION_DIR}/daily/hato-prod-db-20260912T070000Z-22222222-2222-2222-2222-222222222222.dump" ] || fail "Recent backup was incorrectly deleted"
pass "Recent daily backup preserved"

# Monthly promotion: the 45-days-ago backup was promoted to monthly
if [ -f "${RETENTION_DIR}/monthly/hato-prod-db-20260801T070000Z-11111111-1111-1111-1111-111111111111.dump" ]; then
    pass "Monthly backup successfully promoted to monthly retention"
else
    fail "Oldest monthly backup was not promoted to monthly/"
fi

# ------------------------------------------------------------------------------
# 5. Retention: Last Valid Copy Protection
# ------------------------------------------------------------------------------
echo "Running Test 5: Retention protects the ONLY valid copy regardless of age..."
ONLY_DIR="${TEST_DIR}/only_valid_test"
mkdir -p "${ONLY_DIR}/daily" "${ONLY_DIR}/monthly"
# Create only ONE backup, 60 days old
create_backup_set "${ONLY_DIR}/daily" "hato-prod-db-20260715T070000Z-99999999-9999-9999-9999-999999999999" 60

export REMOTE_DAILY_DIR="${ONLY_DIR}/daily"
export REMOTE_MONTHLY_DIR="${ONLY_DIR}/monthly"

"${REPO_ROOT}/scripts/backup-retention.sh" >/dev/null 2>&1

if [ -f "${ONLY_DIR}/daily/hato-prod-db-20260715T070000Z-99999999-9999-9999-9999-999999999999.dump" ]; then
    pass "Sole valid backup protected from deletion despite being 60 days old"
else
    fail "Sole valid backup was deleted by retention policy!"
fi

if [ "${FAILED}" -ne 0 ]; then
    echo "Retention and upload tests FAILED!" >&2
    exit 1
fi

echo "All upload and retention tests PASSED!"
