#!/usr/bin/env bash
# Install FSharp.PureAnalyzer for this workspace:
#   - Prefer nuget.org global package, then always mirror DLL into repo analyzers/
#   - If nuget.org fails, pack + install from the in-repo analyzer via update-analyzer.sh
#
# Ionide/FSAC loads from FSharp.analyzersPath. The workspace-relative entry
# "analyzers" is the reliable path (FSAC does not expand ${userHome} or ~).
set -euo pipefail

# shellcheck source=nuget-tmp-env.sh
source "$(dirname "$0")/nuget-tmp-env.sh"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORKSPACE_ANALYZERS="$ROOT/analyzers/dotnet/fs"
GLOBAL_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

mirror_workspace_drop_from_global() {
  # Find newest installed version under the global packages folder.
  local src schema
  src="$(
    find "$GLOBAL_PACKAGES/fsharp.pureanalyzer" -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' 2>/dev/null \
      | sort -V \
      | tail -1 || true
  )"
  if [[ -z "$src" || ! -f "$src" ]]; then
    return 1
  fi
  schema="$(dirname "$src")/FSharp.PureSchema.dll"
  mkdir -p "$WORKSPACE_ANALYZERS"
  cp -f "$src" "$WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
  if [[ -f "$schema" ]]; then
    cp -f "$schema" "$WORKSPACE_ANALYZERS/FSharp.PureSchema.dll"
  else
    echo "WARNING: FSharp.PureSchema.dll missing next to $src (older package?)" >&2
  fi
  echo "    workspace → $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll (from $src)"
  if [[ -f "$WORKSPACE_ANALYZERS/FSharp.PureSchema.dll" ]]; then
    echo "    workspace → $WORKSPACE_ANALYZERS/FSharp.PureSchema.dll"
  fi
}

install_from_nuget_org() {
  cd "$tmp"
  rm -rf install
  # --no-restore on template creation avoids a restore before TMPDIR is honored
  # by some SDK paths; we restore via `dotnet add package` next.
  dotnet new classlib -n install -f net10.0 --force --language C# >/dev/null
  cd install
  dotnet add package FSharp.PureAnalyzer
}

echo "==> FSharp.PureAnalyzer: try nuget.org"
if install_from_nuget_org; then
  echo "✅ Installed FSharp.PureAnalyzer from nuget.org"
  if mirror_workspace_drop_from_global; then
    echo "✅ Mirrored analyzer into workspace analyzers/ for Ionide"
    exit 0
  fi
  echo "WARNING: nuget install succeeded but could not find DLL to mirror." >&2
fi

echo "    nuget.org path incomplete; packing from local tree."
echo "==> FSharp.PureAnalyzer: pack + install from local tree"
bash "$ROOT/FSharp.PureAnalyzer/update-analyzer.sh"
