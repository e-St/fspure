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
FIXTURE_DIR="$ROOT/e2e/customer-fixture"
ANALYZER_DROP="$FIXTURE_DIR/analyzers/dotnet/fs"
ARTIFACTS="$ROOT/e2e/.artifacts/phase2"
EXT_DIR="$ROOT/vscode-extension"

mkdir -p "$ANALYZER_DROP" "$ARTIFACTS"

echo "==> Phase 2 prepare: build PureAnalyzer"
pushd "$ROOT/FSharp.PureAnalyzer" >/dev/null
if command -v paket >/dev/null 2>&1; then
  paket restore
fi
dotnet build -c "$CONFIGURATION"
cp -f "bin/$CONFIGURATION/net10.0/FSharp.PureAnalyzer.dll" "$ANALYZER_DROP/"
popd >/dev/null
echo "    Analyzer → $ANALYZER_DROP/FSharp.PureAnalyzer.dll"

echo "==> Phase 2 prepare: build customer fixture"
dotnet build "$FIXTURE_DIR/customer-fixture.fsproj" -c "$CONFIGURATION"

echo "==> Phase 2 prepare: package vscode extension VSIX"
if ! command -v vsce >/dev/null 2>&1; then
  npm install -g @vscode/vsce >/dev/null
fi
pushd "$EXT_DIR" >/dev/null
# vsce writes name-version.vsix in cwd
rm -f ./*.vsix
vsce package --allow-missing-repository --out "$ARTIFACTS/fsharp-pure-decorations.vsix"
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
