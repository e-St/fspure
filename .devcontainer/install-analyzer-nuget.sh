#!/usr/bin/env bash
# Install FSharp.PureAnalyzer into the NuGet global packages folder.
# Prefer nuget.org; if the package is not published yet (or install fails),
# pack and install from the in-repo analyzer via update-analyzer.sh.
set -euo pipefail

# shellcheck source=nuget-tmp-env.sh
source "$(dirname "$0")/nuget-tmp-env.sh"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

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
  exit 0
fi

echo "    nuget.org install failed (package may not be published yet)."
echo "==> FSharp.PureAnalyzer: pack + install from local tree"
bash "$ROOT/FSharp.PureAnalyzer/update-analyzer.sh"
