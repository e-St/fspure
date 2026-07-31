#!/usr/bin/env bash
# Install fsharp-pure-decorations.
# Prefer packaging the in-repo VSIX (matches e2e phase2 / known-good labels).
# Fall back to Open VSX if local packaging fails.
#
# Soft-skip when the VS Code CLI is missing or not usable (common in postCreate
# before attach, and in headless CI). postAttachCommand re-runs setup.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXT_DIR="$ROOT/vscode-extension"
PUBLISHER_EXT="e-st.fsharp-pure-decorations"

# True only if `code` is on PATH and can report a version (stubs that print
# "code or code-insiders is not installed" count as unavailable).
code_cli_usable() {
  command -v code >/dev/null 2>&1 || return 1
  local out
  if ! out="$(code --version 2>&1)"; then
    return 1
  fi
  [[ "$out" != *"not installed"* ]] || return 1
  return 0
}

skip_no_code() {
  echo "WARNING: VS Code 'code' CLI not usable; skip extension install." >&2
  echo "         After attach, re-run: bash .devcontainer/setup-fspure-ide.sh" >&2
  exit 0
}

install_from_local() {
  local vsix
  vsix="$(mktemp --suffix=.vsix)"
  # shellcheck disable=SC2064
  trap "rm -f '$vsix'" RETURN
  pushd "$EXT_DIR" >/dev/null
  # vsce packages without publishing; needs network for first npx fetch.
  npx --yes @vscode/vsce package --no-dependencies --allow-missing-repository --out "$vsix"
  popd >/dev/null
  code --install-extension "$vsix" --force
}

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

if ! code_cli_usable; then
  skip_no_code
fi

echo "==> fsharp-pure-decorations: package + install from local tree"
if install_from_local; then
  echo "✅ Installed $PUBLISHER_EXT from local VSIX"
  exit 0
fi

echo "    Local VSIX install failed."
# CLI may have disappeared or become unusable mid-run (e.g. postCreate race).
if ! code_cli_usable; then
  skip_no_code
fi

echo "==> fsharp-pure-decorations: try Open VSX"
if install_from_openvsx; then
  echo "✅ Installed $PUBLISHER_EXT from Open VSX"
  exit 0
fi

if ! code_cli_usable; then
  skip_no_code
fi

echo "ERROR: could not install $PUBLISHER_EXT" >&2
exit 1
