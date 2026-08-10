#!/usr/bin/env bash
# Phase 1 — Analyzer baseline e2e
#
# Builds FSharp.PureAnalyzer, runs it on e2e/customer-fixture/Program.fs via the
# fsharp-analyzers CLI, and compares PURE002/PURE003 definition badges against
# the checked-in baseline (expectations.json).
#
# Usage (from fspure repo root):
#   bash e2e/phase1/run.sh
#
# Env:
#   DOTNET_CONFIGURATION  default: Release
#   UPDATE_BASELINE=1     rewrite expectations.json from current analyzer output

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"
FIXTURE_DIR="$ROOT/e2e/customer-fixture"
ANALYZER_OUT="$ROOT/e2e/.artifacts/analyzer-drop"
REPORT_DIR="$ROOT/e2e/.artifacts/phase1"
SARIF_PATH="$REPORT_DIR/customer-fixture.sarif"
BASELINE="$FIXTURE_DIR/expectations.json"

mkdir -p "$ANALYZER_OUT/dotnet/fs" "$REPORT_DIR"

echo "==> Phase 1: build FSharp.PureAnalyzer ($CONFIGURATION)"
pushd "$ROOT/FSharp.PureAnalyzer" >/dev/null
if command -v paket >/dev/null 2>&1; then
  paket restore
fi
dotnet build -c "$CONFIGURATION"
OUT_DIR="bin/$CONFIGURATION/net10.0"
cp -f "$OUT_DIR/FSharp.PureAnalyzer.dll" "$ANALYZER_OUT/dotnet/fs/"
# Phase 1+: analyser ProjectReferences FSharp.PureSchema — must sit next to the analyzer DLL.
if [[ ! -f "$OUT_DIR/FSharp.PureSchema.dll" ]]; then
  echo "ERROR: FSharp.PureSchema.dll missing next to analyzer at $OUT_DIR" >&2
  exit 1
fi
cp -f "$OUT_DIR/FSharp.PureSchema.dll" "$ANALYZER_OUT/dotnet/fs/"
popd >/dev/null
echo "    DLL → $ANALYZER_OUT/dotnet/fs/FSharp.PureAnalyzer.dll"
echo "    DLL → $ANALYZER_OUT/dotnet/fs/FSharp.PureSchema.dll"

echo "==> Phase 1: build customer fixture"
dotnet build "$FIXTURE_DIR/customer-fixture.fsproj" -c "$CONFIGURATION"

echo "==> Phase 1: ensure fsharp-analyzers CLI"
if ! command -v fsharp-analyzers >/dev/null 2>&1; then
  if [[ -f "$ROOT/dotnet-tools.json" ]] && grep -q 'fsharp-analyzers' "$ROOT/dotnet-tools.json"; then
    dotnet tool restore
    if ! command -v fsharp-analyzers >/dev/null 2>&1; then
      fsharp-analyzers() { dotnet tool run fsharp-analyzers -- "$@"; }
      export -f fsharp-analyzers
    fi
  else
    mkdir -p "$REPORT_DIR/tools"
    dotnet tool install fsharp-analyzers --version 0.35.0 --tool-path "$REPORT_DIR/tools"
    export PATH="$REPORT_DIR/tools:$PATH"
  fi
fi

echo "==> Phase 1: run PureAnalyzer on Program.fs"
set +e
fsharp-analyzers \
  --project "$FIXTURE_DIR/customer-fixture.fsproj" \
  --analyzers-path "$ANALYZER_OUT" \
  --configuration "$CONFIGURATION" \
  --verbosity normal \
  --report "$SARIF_PATH" \
  2>&1 | tee "$REPORT_DIR/analyzer-stdout.txt"
set -e

if [[ ! -f "$SARIF_PATH" ]]; then
  echo "ERROR: SARIF was not written to $SARIF_PATH" >&2
  exit 1
fi

if [[ "${UPDATE_BASELINE:-0}" == "1" ]]; then
  echo "==> Phase 1: UPDATE_BASELINE=1 — rewriting $BASELINE from SARIF"
  python3 "$ROOT/e2e/phase1/assert-definition-badges.py" \
    --sarif "$SARIF_PATH" \
    --expectations "$BASELINE" \
    --write-baseline "$BASELINE"
  echo "    Baseline updated. Review the diff and commit if correct."
  exit 0
fi

echo "==> Phase 1: compare against baseline expectations.json"
python3 "$ROOT/e2e/phase1/assert-definition-badges.py" \
  --sarif "$SARIF_PATH" \
  --expectations "$BASELINE" \
  --write-report "$REPORT_DIR/badge-report.txt"

# Lightweight decoration-code contract (not a visual test — see Phase 2)
if command -v node >/dev/null 2>&1 && [[ -f "$ROOT/vscode-extension/test/decorations.logic.test.js" ]]; then
  echo "==> Phase 1: decoration code→label unit contract"
  node "$ROOT/vscode-extension/test/decorations.logic.test.js"
fi

echo ""
echo "✅ Phase 1 passed — analyzer badges match baseline."
echo "   SARIF:  $SARIF_PATH"
echo "   Report: $REPORT_DIR/badge-report.txt"
