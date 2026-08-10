#!/usr/bin/env bash
# Publish prerelease builds to GitHub Packages only (no nuget.org, no official tags).
# Version: {base}-beta.{run}.{sha7}  or  {base}-ci.{run}.{sha7}
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
require_cmd python3
require_cmd dotnet

cd "$ROOT"

BASE_ANALYZER="$(python3 -c "import json; print(json.load(open('releases/manifest.json'))['lastOfficial']['FSharp.PureAnalyzer'])")"
BASE_COLLECTOR="$(python3 -c "import json; print(json.load(open('releases/manifest.json'))['lastOfficial']['fspure-collector'])")"

SHA7="${GITHUB_SHA:-$(git rev-parse --short=7 HEAD)}"
SHA7="${SHA7:0:7}"
RUN="${GITHUB_RUN_NUMBER:-0}"
KIND="${BETA_KIND:-beta}"  # beta | ci

ANALYZER_VER="${BASE_ANALYZER}-${KIND}.${RUN}.${SHA7}"
COLLECTOR_VER="${BASE_COLLECTOR}-${KIND}.${RUN}.${SHA7}"

echo "Beta versions: analyzer=$ANALYZER_VER collector=$COLLECTOR_VER"

TOKEN="${GITHUB_TOKEN:-${GH_TOKEN:-}}"
[[ -n "$TOKEN" ]] || die "GITHUB_TOKEN required for GitHub Packages"
OWNER="${GITHUB_REPOSITORY_OWNER:-e-St}"
SRC="https://nuget.pkg.github.com/${OWNER}/index.json"

(
  cd FSharp.PureAnalyzer
  if command -v paket >/dev/null 2>&1; then paket restore
  elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then "$HOME/.dotnet/tools/paket" restore
  fi
  dotnet pack -c Release -o ./nupkgs \
    "/p:Version=$ANALYZER_VER" "/p:PackageVersion=$ANALYZER_VER" --nologo
)
dotnet nuget push "FSharp.PureAnalyzer/nupkgs/"*.nupkg \
  --api-key "$TOKEN" --source "$SRC" --skip-duplicate

(
  cd fspure-collector
  if command -v paket >/dev/null 2>&1; then paket restore
  elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then "$HOME/.dotnet/tools/paket" restore
  fi
  dotnet pack -c Release -o ./nupkgs \
    "/p:Version=$COLLECTOR_VER" "/p:PackageVersion=$COLLECTOR_VER" --nologo
)
dotnet nuget push "fspure-collector/nupkgs/"*.nupkg \
  --api-key "$TOKEN" --source "$SRC" --skip-duplicate

echo "Published beta packages to GitHub Packages:"
echo "  FSharp.PureAnalyzer $ANALYZER_VER"
echo "  fspure-collector $COLLECTOR_VER"
