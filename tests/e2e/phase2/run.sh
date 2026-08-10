#!/usr/bin/env bash
# Phase 2 — Visual e2e (devcontainer / code-server + screenshots)
#
# 1) Build analyzer + package decorations VSIX into the fixture workspace
# 2) Start code-server (VS Code Web) with Ionide + pure-decorations
# 3) Open Program.fs via Playwright and capture screenshots of pure/impure labels
#
# Usage (from fspure repo root, inside the phase2 image or any host with deps):
#   bash tests/e2e/phase2/run.sh
#
# Env:
#   CODE_SERVER_HOST  default 127.0.0.1
#   CODE_SERVER_PORT  default 8080
#   WAIT_MS           max wait for badges (default 180000)
#   SKIP_INSTALL_PLAYWRIGHT=1  if browsers already installed

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

ARTIFACTS="$ROOT/tests/e2e/.artifacts/phase2"
PLAYWRIGHT_DIR="$ROOT/tests/e2e/phase2/playwright"
HOST="${CODE_SERVER_HOST:-127.0.0.1}"
PORT="${CODE_SERVER_PORT:-8080}"
export CODE_SERVER_URL="${CODE_SERVER_URL:-http://${HOST}:${PORT}}"
export ARTIFACTS_DIR="$ARTIFACTS"
export WAIT_MS="${WAIT_MS:-180000}"

mkdir -p "$ARTIFACTS"

cleanup() {
  local pid_file="$ARTIFACTS/code-server.pid"
  if [[ -f "$pid_file" ]]; then
    local pid
    pid="$(cat "$pid_file" || true)"
    if [[ -n "${pid:-}" ]] && kill -0 "$pid" 2>/dev/null; then
      echo "==> Stopping code-server pid $pid"
      kill "$pid" || true
    fi
  fi
}
trap cleanup EXIT

echo "======== Phase 2: prepare workspace ========"
bash "$ROOT/tests/e2e/phase2/prepare-workspace.sh"

echo "======== Phase 2: start VS Code (code-server) ========"
bash "$ROOT/tests/e2e/phase2/start-code-server.sh"

echo "======== Phase 2: install Playwright ========"
pushd "$PLAYWRIGHT_DIR" >/dev/null
if [[ ! -d node_modules/playwright ]]; then
  npm install
fi
if [[ "${SKIP_INSTALL_PLAYWRIGHT:-0}" != "1" ]]; then
  npx playwright install chromium
fi
popd >/dev/null

echo "======== Phase 2: open Program.fs and screenshot ========"
node "$PLAYWRIGHT_DIR/screenshot.mjs"

echo ""
echo "✅ Phase 2 finished."
echo "   Screenshots: $ARTIFACTS/*.png"
echo "   Meta:        $ARTIFACTS/screenshot-meta.json"
echo "   Review visually that pure/impure labels sit on the expected definitions."
