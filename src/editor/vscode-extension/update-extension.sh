#!/usr/bin/env bash
# Build local fsharp-pure-decorations and install into the running VS Code / Codespaces.
set -euo pipefail
cd "$(dirname "$0")"
VSIX="$(mktemp --suffix=.vsix)"
trap 'rm -f "$VSIX"' EXIT
npx --yes @vscode/vsce package --no-dependencies --allow-missing-repository --out "$VSIX"
code --install-extension "$VSIX" --force
echo "✅ Installed fsharp-pure-decorations from local VSIX — reload window if badges do not appear."
