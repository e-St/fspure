#!/usr/bin/env bash
# Prepare the customer fixture workspace for Phase 2 visual capture:
#   - build PureAnalyzer and drop DLL under fixture/analyzers
#   - package fsharp-pure-decorations.vsix
#   - restore/build the fixture project (Ionide needs a loadable project)
#
# Run from fspure repo root (inside the phase2 devcontainer or CI).

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"
FIXTURE_DIR="$ROOT/tests/e2e/customer-fixture"
ANALYZER_DROP="$FIXTURE_DIR/analyzers/dotnet/fs"
ARTIFACTS="$ROOT/tests/e2e/.artifacts/phase2"
EXT_DIR="$ROOT/vscode-extension"

mkdir -p "$ANALYZER_DROP" "$ARTIFACTS"

echo "==> Phase 2 prepare: build PureAnalyzer"
pushd "$ROOT/FSharp.PureAnalyzer" >/dev/null
if command -v paket >/dev/null 2>&1; then
  paket restore
fi
dotnet build -c "$CONFIGURATION"
OUT_DIR="bin/$CONFIGURATION/net10.0"
cp -f "$OUT_DIR/FSharp.PureAnalyzer.dll" "$ANALYZER_DROP/"
if [[ ! -f "$OUT_DIR/FSharp.PureSchema.dll" ]]; then
  echo "ERROR: FSharp.PureSchema.dll missing next to analyzer at $OUT_DIR" >&2
  exit 1
fi
cp -f "$OUT_DIR/FSharp.PureSchema.dll" "$ANALYZER_DROP/"
popd >/dev/null
echo "    Analyzer → $ANALYZER_DROP/FSharp.PureAnalyzer.dll"
echo "    Schema   → $ANALYZER_DROP/FSharp.PureSchema.dll"

echo "==> Phase 2 prepare: build customer fixture (solution)"
SOLUTION="$FIXTURE_DIR/customer-fixture.slnx"
if [[ ! -f "$SOLUTION" ]]; then
  echo "ERROR: missing $SOLUTION (Ionide needs a solution like the consumer codespace)" >&2
  exit 1
fi
dotnet build "$SOLUTION" -c "$CONFIGURATION"
echo "    Solution → $SOLUTION"

echo "==> Phase 2 prepare: package vscode extension VSIX"
pushd "$EXT_DIR" >/dev/null
# vsce writes name-version.vsix in cwd
rm -f ./*.vsix
# Use npx (not npm install -g) so packaging works without write access
# to system node_modules (EACCES on /usr/lib/node_modules in the phase2 image).
npx --yes @vscode/vsce package --allow-missing-repository --out "$ARTIFACTS/fsharp-pure-decorations.vsix"
popd >/dev/null
echo "    VSIX → $ARTIFACTS/fsharp-pure-decorations.vsix"

# Record paths for other scripts
cat > "$ARTIFACTS/paths.env" <<EOF
FIXTURE_DIR=$FIXTURE_DIR
ANALYZER_DROP=$ANALYZER_DROP
VSIX_PATH=$ARTIFACTS/fsharp-pure-decorations.vsix
ARTIFACTS=$ARTIFACTS
ROOT=$ROOT
EOF

echo "✅ Phase 2 workspace prepared."
