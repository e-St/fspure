#!/usr/bin/env bash
# Back-compat wrapper. Prefer the phased entrypoints:
#   bash e2e/phase1/run.sh
#   bash e2e/phase2/run.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
echo "NOTE: run-customer-e2e.sh now runs Phase 1 only."
echo "      For visual screenshots, run: bash e2e/phase2/run.sh"
bash "$ROOT/e2e/phase1/run.sh"
