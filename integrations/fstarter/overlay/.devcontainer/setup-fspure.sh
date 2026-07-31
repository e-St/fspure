#!/usr/bin/env bash
# Install fspure into an fstarter Codespace / dev container.
# - FSharp.PureAnalyzer from nuget.org → workspace analyzers/dotnet/fs/
# - fsharp-pure-decorations from Open VSX VSIX (not MS Marketplace id)
set -euo pipefail

if [[ "${SKIP_FSPURE_SETUP:-}" == "1" ]]; then
  echo "Skipping fspure setup (SKIP_FSPURE_SETUP=1)."
  exit 0
fi

export TMPDIR="${TMPDIR:-${HOME}/.cache/nuget-tmp}"
export TEMP="${TEMP:-$TMPDIR}"
export TMP="${TMP:-$TMPDIR}"
mkdir -p "$TMPDIR"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORKSPACE_ANALYZERS="$ROOT/analyzers/dotnet/fs"
GLOBAL_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
PUBLISHER_EXT="e-st.fsharp-pure-decorations"
OPENVSX_API="https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest"

code_cli_usable() {
  command -v code >/dev/null 2>&1 || return 1
  local out
  if ! out="$(code --version 2>&1)"; then
    return 1
  fi
  [[ "$out" != *"not installed"* ]] || return 1
  return 0
}

mirror_from_global() {
  local src
  src="$(
    find "$GLOBAL_PACKAGES/fsharp.pureanalyzer" \
      -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' 2>/dev/null \
      | sort -V \
      | tail -1 || true
  )"
  if [[ -z "$src" || ! -f "$src" ]]; then
    return 1
  fi
  mkdir -p "$WORKSPACE_ANALYZERS"
  cp -f "$src" "$WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
  echo "    workspace → $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
}

install_analyzer_nuget() {
  local tmp
  tmp="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$tmp'" RETURN
  echo "==> FSharp.PureAnalyzer: nuget.org"
  (
    cd "$tmp"
    # Quiet via redirect — `dotnet add package` does not accept --verbosity on all SDKs.
    dotnet new classlib -n install -f net10.0 --force --language C# >/dev/null
    cd install
    dotnet add package FSharp.PureAnalyzer >/dev/null
  )
}

install_extension_openvsx() {
  local vsix url
  vsix="$(mktemp --suffix=.vsix)"
  # shellcheck disable=SC2064
  trap "rm -f '$vsix'" RETURN
  echo "==> fsharp-pure-decorations: Open VSX VSIX"
  url="$(curl -fsSL "$OPENVSX_API" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
  curl -fsSL -o "$vsix" "$url"
  code --install-extension "$vsix" --force
}

echo "==> fspure setup (fstarter)"

if install_analyzer_nuget && mirror_from_global; then
  echo "✅ FSharp.PureAnalyzer installed and mirrored for Ionide"
else
  echo "ERROR: could not install or mirror FSharp.PureAnalyzer from nuget.org" >&2
  exit 1
fi

if code_cli_usable; then
  if install_extension_openvsx; then
    echo "✅ Installed $PUBLISHER_EXT (Open VSX VSIX)"
  else
    echo "WARNING: Open VSX VSIX install failed." >&2
  fi
else
  echo "WARNING: VS Code 'code' CLI not usable; skip extension install." >&2
fi

echo ""
echo "✅ fspure setup done."
echo "   Analyzer: $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
echo "   If pure/impure labels are missing: Developer: Reload Window"
