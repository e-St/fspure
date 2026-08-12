#!/usr/bin/env bash
# fspure IDE setup (root .devcontainer only).
#
# Install analyzer + decorations extension for interactive Codespaces / VS Code.
#
# Analyzer: nuget.org when available, else pack this repo; always mirror the
# DLL into <repo>/analyzers/dotnet/fs/ so Ionide's default path "analyzers"
# finds it (FSAC does not expand ${userHome} or ~ in FSharp.analyzersPath).
#
# Extension: package in-repo VSIX (same packaging path as e2e phase2), else Open VSX.
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

cd "$(dirname "$0")"
bash install-analyzer-nuget.sh
bash install-openvsx-extension.sh
bash install-fspure-skill.sh

echo ""
echo "✅ fspure IDE setup done."
echo "   Analyzer drop: $(cd .. && pwd)/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
echo "   If pure/impure labels are missing: Developer: Reload Window"
