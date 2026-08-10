#!/usr/bin/env bash
# Phase 2 — Visual e2e (code-server + F# / Playwright.NET screenshots)
#
# Usage (repo root):
#   bash tests/e2e/phase2/run.sh
#
# Env: CODE_SERVER_HOST, CODE_SERVER_PORT, WAIT_MS, SKIP_INSTALL_PLAYWRIGHT=1

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

ARTIFACTS="$ROOT/tests/e2e/.artifacts/phase2"
SHOT_PROJ="$ROOT/tests/e2e/phase2/ScreenshotCapture/ScreenshotCapture.fsproj"
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

echo "======== Phase 2: build F# Playwright capture ========"
dotnet build "$SHOT_PROJ" -c Release --nologo -v q

if [[ "${SKIP_INSTALL_PLAYWRIGHT:-0}" != "1" ]]; then
  echo "======== Phase 2: install Chromium (Playwright.NET) ========"
  PW_PS1="$(find "$ROOT/tests/e2e/phase2/ScreenshotCapture/bin" -name 'playwright.ps1' 2>/dev/null | head -1 || true)"
  if [[ -n "${PW_PS1:-}" ]] && command -v pwsh >/dev/null 2>&1; then
    pwsh -NoProfile -File "$PW_PS1" install chromium
  else
    # Install CLI once, then browsers
    if ! command -v playwright >/dev/null 2>&1; then
      dotnet tool install --global Microsoft.Playwright.CLI --version 1.61.0 2>/dev/null \
        || dotnet tool update --global Microsoft.Playwright.CLI --version 1.61.0 2>/dev/null \
        || true
      export PATH="${PATH}:${HOME}/.dotnet/tools"
    fi
    playwright install chromium || true
  fi
fi

echo "======== Phase 2: open Program.fs and screenshot (F#) ========"
dotnet run --project "$SHOT_PROJ" -c Release --no-build

echo ""
echo "✅ Phase 2 finished."
echo "   Screenshots: $ARTIFACTS/*.png"
echo "   Meta:        $ARTIFACTS/screenshot-meta.json"
