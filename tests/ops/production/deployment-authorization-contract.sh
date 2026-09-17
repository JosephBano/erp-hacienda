#!/usr/bin/env bash
# Contract test for the production deployment authorization boundary.
# It intentionally has no network or secret dependency.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
entry="$repo_root/scripts/production-deploy-entry.sh"
workflow="$repo_root/.github/workflows/deploy-production.yml"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

for required in run_id run_attempt authorization; do
    grep -Fq "\"$required\"" "$entry" \
        || fail "deploy-entry must require manifest field $required"
done

grep -Fq 'ssh-keygen -Y verify' "$entry" \
    || fail 'deploy-entry must verify the host-side deployment authorization signature'
grep -Fq 'DEPLOY_AUTHORIZATION_KEY' "$workflow" \
    || fail 'workflow must use the environment-scoped authorization signing key'
grep -Fq 'github.run_id' "$workflow" \
    || fail 'workflow must bind authorization to the GitHub run id'
grep -Fq 'github.run_attempt' "$workflow" \
    || fail 'workflow must bind authorization to the GitHub run attempt'

echo 'PASS: deployment authorization contract'
