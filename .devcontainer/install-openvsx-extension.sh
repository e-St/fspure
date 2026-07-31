#!/usr/bin/env bash
# Install fsharp-pure-decorations.
# Prefer Open VSX; if that fails (not published / offline), package and install
# the in-repo vscode-extension VSIX instead.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXT_DIR="$ROOT/vscode-extension"
PUBLISHER_EXT="e-st.fsharp-pure-decorations"

install_from_openvsx() {
  local vsix
  vsix="$(mktemp --suffix=.vsix)"
  # shellcheck disable=SC2064
  trap "rm -f '$vsix'" RETURN
  local url
  url="$(curl -fsSL "https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
  curl -fsSL -o "$vsix" "$url"
  code --install-extension "$vsix" --force
}

install_from_local() {
  pushd "$EXT_DIR" >/dev/null
  # vsce packages without publishing; needs network for first npx fetch.
  npx --yes @vscode/vsce package --no-dependencies --out /tmp/fsharp-pure-decorations-local.vsix
  popd >/dev/null
  code --install-extension /tmp/fsharp-pure-decorations-local.vsix --force
  rm -f /tmp/fsharp-pure-decorations-local.vsix
}

if ! command -v code >/dev/null 2>&1; then
  echo "WARNING: 'code' CLI not on PATH; skip VS Code extension install." >&2
  echo "         After attach, re-run: bash .devcontainer/setup-fspure-ide.sh" >&2
  exit 0
fi

echo "==> fsharp-pure-decorations: try Open VSX"
if install_from_openvsx; then
  echo "✅ Installed $PUBLISHER_EXT from Open VSX"
  exit 0
fi

echo "    Open VSX install failed (extension may not be published yet)."
echo "==> fsharp-pure-decorations: package + install from local tree"
install_from_local
echo "✅ Installed $PUBLISHER_EXT from local VSIX"
