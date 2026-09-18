#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# ERP Hacienda - Fail-Closed Backup and Restore Verification Tests
# ==============================================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
TEST_DIR="$(mktemp -d /tmp/hato-backup-fail-closed-test.XXXXXX)"
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
# 1. Backup script fails closed when dump command fails
# ------------------------------------------------------------------------------
echo "Running Test 1: Backup script fails closed when dump command fails..."
export BACKUP_DIR="${TEST_DIR}/backups"
mkdir -p "${BACKUP_DIR}"

# Force failure using an invalid helper
export BACKUP_HELPER="false"
if "${REPO_ROOT}/scripts/backup.sh" >"${TEST_DIR}/backup_fail.log" 2>&1; then
    fail "backup.sh should have exited non-zero when dump command failed"
else
    # Check that no .dump file was produced and no .partial remains
    if ls "${BACKUP_DIR}"/*.dump >/dev/null 2>&1; then
        fail "backup.sh created a .dump file despite command failure"
    elif ls "${BACKUP_DIR}"/*.partial >/dev/null 2>&1; then
        fail "backup.sh left a .partial file on disk after failure"
    else
        pass "backup.sh failed closed without leaving partial or invalid dump files"
    fi
fi

# ------------------------------------------------------------------------------
# 3b. Restore requires the manifest, not only a checksum
# ------------------------------------------------------------------------------
echo "Running Test 3b: Restore fails when manifest is missing..."
MANIFEST_DUMP="${TEST_DIR}/no_manifest.dump"
printf 'fake dump content' > "${MANIFEST_DUMP}"
sha256sum "${MANIFEST_DUMP}" > "${MANIFEST_DUMP%.dump}.sha256"
if "${REPO_ROOT}/scripts/restore.sh" "${MANIFEST_DUMP}" "test_restore_db" >"${TEST_DIR}/restore_no_manifest.log" 2>&1; then
    fail "restore.sh should require a manifest for every backup"
else
    grep -Fq 'manifiesto' "${TEST_DIR}/restore_no_manifest.log" || fail "restore.sh did not explain the missing manifest"
    pass "restore.sh rejected dump without manifest"
fi
unset BACKUP_HELPER

# ------------------------------------------------------------------------------
# 2. Restore rejects .partial file
# ------------------------------------------------------------------------------
echo "Running Test 2: Restore rejects .partial file..."
PARTIAL_DUMP="${TEST_DIR}/test_backup.dump.partial"
echo "partial data" > "${PARTIAL_DUMP}"
if "${REPO_ROOT}/scripts/restore.sh" "${PARTIAL_DUMP}" "test_restore_db" >"${TEST_DIR}/restore_partial.log" 2>&1; then
    fail "restore.sh should reject a .partial file"
else
    pass "restore.sh rejected .partial file with non-zero exit code"
fi

# ------------------------------------------------------------------------------
# 3. Restore fails when checksum file is missing
# ------------------------------------------------------------------------------
echo "Running Test 3: Restore fails when checksum file is missing..."
DUMP_NO_CHECKSUM="${TEST_DIR}/no_checksum.dump"
echo "fake dump content" > "${DUMP_NO_CHECKSUM}"
if "${REPO_ROOT}/scripts/restore.sh" "${DUMP_NO_CHECKSUM}" "test_restore_db" >"${TEST_DIR}/restore_no_chk.log" 2>&1; then
    fail "restore.sh should fail when .sha256 file is missing"
else
    pass "restore.sh rejected dump without checksum"
fi

# ------------------------------------------------------------------------------
# 4. Restore fails when checksum is mismatched / corrupted
# ------------------------------------------------------------------------------
echo "Running Test 4: Restore fails when checksum is mismatched..."
CORRUPT_DUMP="${TEST_DIR}/corrupt.dump"
echo "corrupt dump content" > "${CORRUPT_DUMP}"
(cd "${TEST_DIR}" && echo "badhash1234567890abcdef1234567890abcdef1234567890abcdef1234567890  corrupt.dump" > "${CORRUPT_DUMP}.sha256")
if "${REPO_ROOT}/scripts/restore.sh" "${CORRUPT_DUMP}" "test_restore_db" >"${TEST_DIR}/restore_corrupt_chk.log" 2>&1; then
    fail "restore.sh should fail when SHA256 checksum does not match"
else
    pass "restore.sh rejected corrupted dump with mismatched checksum"
fi

# ------------------------------------------------------------------------------
# 5. Restore guards production database name
# ------------------------------------------------------------------------------
echo "Running Test 5: Restore guards production database name..."
VALID_DUMP="${TEST_DIR}/valid.dump"
echo "valid dummy content" > "${VALID_DUMP}"
(cd "${TEST_DIR}" && sha256sum "valid.dump" > "${VALID_DUMP}.sha256")

if "${REPO_ROOT}/scripts/restore.sh" "${VALID_DUMP}" "hato_production" >"${TEST_DIR}/restore_prod.log" 2>&1; then
    fail "restore.sh should refuse to restore into hato_production without --force-production-restore-disaster-only"
else
    pass "restore.sh guarded production database destination"
fi

# ------------------------------------------------------------------------------
# 6. Manifest verification detects tampering
# ------------------------------------------------------------------------------
echo "Running Test 6: Manifest verification detects tampering..."
MANIFEST_FILE="${TEST_DIR}/test.manifest.json"
"${REPO_ROOT}/scripts/backup-manifest.sh" generate "${VALID_DUMP}" "${MANIFEST_FILE}" "production" "hato_production" "dummy_sha"

# Verify passes on untouched dump
if ! "${REPO_ROOT}/scripts/backup-manifest.sh" verify "${MANIFEST_FILE}" "${VALID_DUMP}" >/dev/null 2>&1; then
    fail "backup-manifest.sh verify failed on valid untouched dump"
else
    pass "backup-manifest.sh verify passed on valid dump"
fi

# Alter dump content (tampering)
echo "extra bytes" >> "${VALID_DUMP}"
if "${REPO_ROOT}/scripts/backup-manifest.sh" verify "${MANIFEST_FILE}" "${VALID_DUMP}" >/dev/null 2>&1; then
    fail "backup-manifest.sh verify should have failed after dump tampering"
else
    pass "backup-manifest.sh verify detected tampered dump"
fi

if [ "${FAILED}" -ne 0 ]; then
    echo "Fail-closed tests FAILED!" >&2
    exit 1
fi

echo "All fail-closed tests PASSED!"
