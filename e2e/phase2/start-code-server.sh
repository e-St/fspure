#!/usr/bin/env bash
# Start code-server (VS Code Web) against the customer fixture workspace.
# Installs Ionide + the locally built pure-decorations VSIX.
# Applies consumer-style Ionide / decoration settings
# (see e2e/customer-fixture/.vscode/settings.json).
#
# Ionide only runs analyzers / inlay hints after a solution is loaded — same as
# the manual flow: pick customer-fixture.slnx, then open Program.fs.

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
ANALYZER_DIR="$FIXTURE_DIR/analyzers"
SOLUTION_PATH="$FIXTURE_DIR/customer-fixture.slnx"
PROJECT_PATH="$FIXTURE_DIR/customer-fixture.fsproj"

if [[ ! -f "$SOLUTION_PATH" ]]; then
  echo "ERROR: missing $SOLUTION_PATH" >&2
  exit 1
fi
if [[ ! -f "$ANALYZER_DIR/dotnet/fs/FSharp.PureAnalyzer.dll" ]]; then
  echo "ERROR: PureAnalyzer DLL missing under $ANALYZER_DIR (run prepare-workspace.sh first)" >&2
  exit 1
fi
if [[ ! -f "$ANALYZER_DIR/dotnet/fs/FSharp.PureSchema.dll" ]]; then
  echo "ERROR: FSharp.PureSchema.dll missing under $ANALYZER_DIR (required dependency of PureAnalyzer)" >&2
  exit 1
fi

echo "==> Install extensions into code-server"
# Ionide (F# language service + analyzer host). On Open VSX this also pulls
# muhammad-sammy.csharp (free C# extension) as a dependency.
code-server \
  --user-data-dir "$USER_DATA" \
  --extensions-dir "$EXT_DIR" \
  --install-extension ionide.ionide-fsharp \
  --force

# Optional companion extensions from the consumer codespace (best-effort).
for ext in ionide.ionide-paket ionide.ionide-fantomas; do
  code-server \
    --user-data-dir "$USER_DATA" \
    --extensions-dir "$EXT_DIR" \
    --install-extension "$ext" \
    --force \
    || echo "    (skip optional $ext)"
done

code-server \
  --user-data-dir "$USER_DATA" \
  --extensions-dir "$EXT_DIR" \
  --install-extension "$VSIX" \
  --force

# Fresh user settings each run (avoid stale Ionide workspace choice).
# Absolute FSharp.workspacePath + dotnet.defaultSolution match the manual
# "select the project's slnx" step so FSAC loads without a picker.
mkdir -p "$USER_DATA/User"
cat > "$USER_DATA/User/settings.json" <<EOF
{
  "security.workspace.trust.enabled": false,
  "security.workspace.trust.startupPrompt": "never",
  "security.workspace.trust.banner": "never",
  "telemetry.telemetryLevel": "off",
  "update.mode": "none",
  "extensions.autoUpdate": false,
  "extensions.autoCheckUpdates": false,
  "workbench.startupEditor": "none",
  "workbench.colorTheme": "Default Dark Modern",
  "workbench.tips.enabled": false,
  "workbench.welcomePage.walkthroughs.openOnInstall": false,
  "extensions.ignoreRecommendations": true,
  "git.openRepositoryInParentFolders": "never",

  "editor.inlineSuggest.enabled": false,
  "editor.parameterHints.enabled": false,
  "editor.acceptSuggestionOnEnter": "off",
  "[fsharp]": {
    "editor.quickSuggestions": false,
    "editor.suggestOnTriggerCharacters": false
  },
  "editor.inlayHints.enabled": "on",
  "editor.formatOnSave": true,
  "editor.minimap.enabled": false,
  "editor.fontSize": 14,
  "editor.lineHeight": 22,

  "dotnet.defaultSolution": "$SOLUTION_PATH",
  "FSharp.workspacePath": "$SOLUTION_PATH",
  "FSharp.workspaceModePeekDeepLevel": 1,
  "FSharp.showExplorerOnStartup": true,
  "FSharp.enableMSBuildProjectGraph": true,

  "FSharp.inlayHints.enabled": true,
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.inlayHints.parameterNames": true,

  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "// ",
  "FSharp.pipelineHints.enabled": true,
  "FSharp.pipelineHints.prefix": "// ",

  "FSharp.linter": true,
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers",
    "$ANALYZER_DIR"
  ],
  "FSharp.unusedDeclarationsAnalyzer": true,
  "FSharp.codeLenses.references.enabled": false,

  "files.exclude": {
    "**/obj": true,
    "**/bin": true,
    "**/.paket": true
  },

  "fsharpPureDecorations.enabled": true,
  "fsharpPureDecorations.impureColor": "#E2A66A",
  "fsharpPureDecorations.pureColor": "#6A9955",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
EOF

echo "    workspace settings: $FIXTURE_DIR/.vscode/settings.json"
echo "    solution: $SOLUTION_PATH"

# Stop previous instance if any
if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
  echo "    Stopping previous code-server pid $(cat "$PID_FILE")"
  kill "$(cat "$PID_FILE")" || true
  sleep 1
fi

# Clear prior workspaceStorage so Ionide re-reads FSharp.workspacePath
rm -rf "$USER_DATA/User/workspaceStorage" "$USER_DATA/CachedConfigurations" 2>/dev/null || true

echo "==> Start code-server on http://${HOST}:${PORT}"
echo "    workspace: $FIXTURE_DIR"
echo "    analyzer:  $ANALYZER_DIR/dotnet/fs/FSharp.PureAnalyzer.dll"
echo "    project:   $PROJECT_PATH"
echo "    solution:  $SOLUTION_PATH"
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
