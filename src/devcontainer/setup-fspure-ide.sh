#!/usr/bin/env bash
# fspure IDE setup (root .devcontainer only).
#
# Install analyzer + decorations extension for interactive Codespaces / VS Code.
#
# Analyzer: nuget.org when available, else pack this repo; always mirror the
# DLL into <repo>/analyzers/dotnet/fs/ so Ionide's default path "analyzers"
# finds it (FSAC does not expand ${userHome} or ~ in FSharp.analyzersPath).
#
# Extension: package in-repo VSIX (src/editor/vscode-extension), else baked
# /usr/local/share/fspure/fsharp-pure-decorations.vsix, else Open VSX.
# Always unpack onto disk (code CLI is often unusable in postCreate).
#
# Not used by CI: analyzer pack/build uses src/FSharp.PureAnalyzer/.devcontainer/;
# e2e uses src/tests/e2e/phase2/.devcontainer/ (see .devcontainer/README.md).
# Optional escape hatch: SKIP_FSPURE_IDE_SETUP=1.
set -euo pipefail

if [[ "${SKIP_FSPURE_IDE_SETUP:-}" == "1" ]]; then
  echo "Skipping fspure IDE setup (SKIP_FSPURE_IDE_SETUP=1)."
  exit 0
fi

# shellcheck source=nuget-tmp-env.sh
source "$(dirname "$0")/nuget-tmp-env.sh"

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
cd "$HERE"
bash install-analyzer-nuget.sh
bash install-openvsx-extension.sh
bash install-fspure-cli.sh
bash install-fspure-skill.sh

echo ""
echo "✅ fspure IDE setup done."
echo "   Analyzer drop: $ROOT/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
echo "   If pure/impure labels are missing: Developer: Reload Window"
