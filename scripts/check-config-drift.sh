#!/usr/bin/env bash
# ==============================================================================
# check-config-drift.sh
# Wrapper de conveniencia para check-config-drift.py
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "$SCRIPT_DIR/check-config-drift.py" "$@"
