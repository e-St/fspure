#!/usr/bin/env bash
# fspure IDE setup (root .devcontainer only).
#
# Install analyzer + decorations extension for interactive Codespaces / VS Code.
#
# Analyzer: nuget.org at the versions.env pin, else pack this repo; always
# mirror that DLL into <repo>/analyzers/dotnet/fs/. Ionide's workspace path
# "analyzers" is listed ahead of the image bake (FSAC does not expand
# ${userHome} or ~ in FSharp.analyzersPath).
#
# Extension: package the in-repo VSIX when package.json matches the pin,
# else the pinned Open VSX version. A baked VSIX is used only when it matches.
# An older on-disk extension is not treated as current.
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
