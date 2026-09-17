#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
runbook="$repo_root/ops/production/bootstrap.md"

if ! rg -F -q 'm 644 /dev/stdin /etc/ssh/authorized_keys/hato-deploy' "$runbook"; then
    echo 'FAIL: the root-owned AuthorizedKeysFile must be installed mode 644 so sshd can read it as hato-deploy.' >&2
    exit 1
fi

echo 'PASS: deploy AuthorizedKeysFile is root-owned and readable by sshd.'
