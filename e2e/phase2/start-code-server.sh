#!/usr/bin/env bash
# Start code-server (VS Code Web) against the customer fixture workspace.
# Installs Ionide + the locally built pure-decorations VSIX.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARTIFACTS="$ROOT/e2e/.artifacts/phase2"
FIXTURE_DIR="$ROOT/e2e/customer-fixture"
HOST="${CODE_SERVER_HOST:-127.0.0.1}"
PORT="${CODE_SERVER_PORT:-8080}"
LOG="$ARTIFACTS/code-server.log"
PID_FILE="$ARTIFACTS/code-server.pid"
USER_DATA="$ARTIFACTS/code-server-user-data"
EXT_DIR="$ARTIFACTS/code-server-extensions"

mkdir -p "$ARTIFACTS" "$USER_DATA" "$EXT_DIR"

if [[ ! -f "$ARTIFACTS/fsharp-pure-decorations.vsix" ]]; then
  bash "$ROOT/e2e/phase2/prepare-workspace.sh"
fi

VSIX="$ARTIFACTS/fsharp-pure-decorations.vsix"

echo "==> Install extensions into code-server"
# Ionide (F# language service + analyzer host)
code-server \
  --user-data-dir "$USER_DATA" \
  --extensions-dir "$EXT_DIR" \
  --install-extension ionide.ionide-fsharp \
  --force

code-server \
  --user-data-dir "$USER_DATA" \
  --extensions-dir "$EXT_DIR" \
  --install-extension "$VSIX" \
  --force

# Disable workspace trust / telemetry noise via argv-like product settings
mkdir -p "$USER_DATA/User"
cat > "$USER_DATA/User/settings.json" <<'EOF'
{
  "security.workspace.trust.enabled": false,
  "security.workspace.trust.startupPrompt": "never",
  "security.workspace.trust.banner": "never",
  "telemetry.telemetryLevel": "off",
  "update.mode": "none",
  "extensions.autoUpdate": false,
  "extensions.autoCheckUpdates": false,
  "workbench.startupEditor": "none",
  "workbench.colorTheme": "Default Dark Modern"
}
EOF

# Stop previous instance if any
if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "    Stopping previous code-server pid $(cat "$PID_FILE")"
  kill "$(cat "$PID_FILE")" || true
  sleep 1
fi

echo "==> Start code-server on http://${HOST}:${PORT}"
echo "    workspace: $FIXTURE_DIR"
nohup code-server \
  --auth none \
  --bind-addr "${HOST}:${PORT}" \
  --user-data-dir "$USER_DATA" \
  --extensions-dir "$EXT_DIR" \
  --disable-telemetry \
  --disable-update-check \
  "$FIXTURE_DIR" \
  >"$LOG" 2>&1 &
echo $! >"$PID_FILE"

# Wait until HTTP responds
for i in $(seq 1 60); do
  if curl -fsS "http://${HOST}:${PORT}/" >/dev/null 2>&1; then
    echo "    code-server is up (pid $(cat "$PID_FILE"))"
    exit 0
  fi
  sleep 1
done

echo "ERROR: code-server did not become ready. Log tail:" >&2
tail -n 80 "$LOG" >&2 || true
exit 1
