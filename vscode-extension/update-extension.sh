#!/usr/bin/env bash
# Build local fsharp-pure-decorations and install into the running VS Code / Codespaces.
set -euo pipefail
cd "$(dirname "$0")"
VSIX="$(mktemp --suffix=.vsix)"
trap 'rm -f "$VSIX"' EXIT
npx --yes @vscode/vsce package --no-dependencies --out "$VSIX"
code --install-extension "$VSIX" --force
