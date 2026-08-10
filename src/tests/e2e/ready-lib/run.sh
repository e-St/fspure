#!/usr/bin/env bash
# Monorepo e2e for the library-embed story (Phase 4).
# Thin wrapper: same gate as CI (local feed only).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
exec bash "$ROOT/src/scripts/fspure-ready-lib-gate.sh"
