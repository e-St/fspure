#!/usr/bin/env bash
# Bake fspure CLI, analyzer DLLs, Copilot skill, and decorations VSIX into the image.
# Copied by prepare-fstarter-update.sh. Pins come from versions.env.
#
# nuget.org can accept a push minutes before the flat container lists the nupkg.
# curl --retry does not retry HTTP 404, so a short retry still fails the image
# build (fstarter CI, 0.4.2). Poll the flat container, then fall back to the
# GitHub Release asset uploaded by the official publish.
set -euo pipefail

PREFIX="${FSPURE_BAKE_PREFIX:-/usr/local}"
VERSIONS="${FSPURE_VERSIONS_FILE:-${PREFIX}/share/fspure/versions.env}"
UA="e-St-fstarter-image-bake"
NUGET_WAIT="${FSPURE_NUGET_WAIT_SECONDS:-900}"
NUGET_INTERVAL="${FSPURE_NUGET_WAIT_INTERVAL:-15}"

set -a
# shellcheck disable=SC1090
. "$VERSIONS"
set +a

: "${FSPURE_ANALYZER_VERSION:?missing FSPURE_ANALYZER_VERSION in $VERSIONS}"
: "${FSPURE_SKILL_REF:?missing FSPURE_SKILL_REF in $VERSIONS}"
: "${FSPURE_CLI_RELEASE:?missing FSPURE_CLI_RELEASE in $VERSIONS}"

TOKEN="${GH_TOKEN:-${GITHUB_TOKEN:-}}"

echo "==> bake fspure"
echo "    versions=${VERSIONS}"
echo "    analyzer=${FSPURE_ANALYZER_VERSION}"
echo "    skill_ref=${FSPURE_SKILL_REF}"
echo "    cli_release=${FSPURE_CLI_RELEASE}"
echo "    github_auth=$([ -n "$TOKEN" ] && echo yes || echo no)"

mkdir -p \
  "${PREFIX}/bin" \
  "${PREFIX}/share/fspure/analyzers/dotnet/fs" \
  "${PREFIX}/share/fspure/skills/fspure-reduce-impurity"

auth_headers=()
if [[ -n "$TOKEN" ]]; then
  auth_headers=(-H "Authorization: Bearer ${TOKEN}")
fi

# GitHub Actions 404s anonymous github.com/releases/download. Use the API + asset URL.
echo "==> fspure CLI tag=${FSPURE_CLI_RELEASE}"
api="https://api.github.com/repos/e-St/fspure/releases/tags/${FSPURE_CLI_RELEASE}"
echo "    GET ${api}"
asset_api="$(
  curl -fsSL --retry 3 --retry-delay 2 \
    -H "User-Agent: ${UA}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "${auth_headers[@]}" \
    "$api" \
    | python3 -c '
import json, sys
rel = json.load(sys.stdin)
for a in rel.get("assets") or []:
    if a.get("name") == "fspure":
        print(a["url"])
        sys.exit(0)
sys.exit("release has no asset named fspure")
'
)"
echo "    GET ${asset_api} (octet-stream)"
curl -fsSL --retry 3 --retry-delay 2 \
  -H "User-Agent: ${UA}" \
  -H "Accept: application/octet-stream" \
  "${auth_headers[@]}" \
  -L -o "${PREFIX}/bin/fspure" \
  "$asset_api"
chmod +x "${PREFIX}/bin/fspure"

download_analyzer_nupkg() {
  local dest="$1"
  local nupkg_url="https://api.nuget.org/v3-flatcontainer/fsharp.pureanalyzer/${FSPURE_ANALYZER_VERSION}/fsharp.pureanalyzer.${FSPURE_ANALYZER_VERSION}.nupkg"
  local deadline=$((SECONDS + NUGET_WAIT))
  echo "==> FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION}"
  echo "    GET ${nupkg_url}"
  while true; do
    if curl -fsSL --retry 2 --retry-delay 2 --retry-all-errors \
      -H "User-Agent: ${UA}" -o "$dest" "$nupkg_url"; then
      return 0
    fi
    if (( SECONDS >= deadline )); then
      echo "    nuget.org still not serving ${FSPURE_ANALYZER_VERSION}; trying GitHub release asset"
      break
    fi
    echo "    nuget.org not ready (404 is not retried by curl --retry); retrying in ${NUGET_INTERVAL}s"
    sleep "$NUGET_INTERVAL"
  done

  local tag="v${FSPURE_ANALYZER_VERSION}"
  local rel_api="https://api.github.com/repos/e-St/fspure/releases/tags/${tag}"
  local asset_name="FSharp.PureAnalyzer.${FSPURE_ANALYZER_VERSION}.nupkg"
  echo "    GET ${rel_api}"
  local gh_asset
  gh_asset="$(
    curl -fsSL --retry 5 --retry-delay 2 --retry-all-errors \
      -H "User-Agent: ${UA}" \
      -H "Accept: application/vnd.github+json" \
      -H "X-GitHub-Api-Version: 2022-11-28" \
      "${auth_headers[@]}" \
      "$rel_api" \
      | ASSET_NAME="$asset_name" python3 -c '
import json, os, sys
name = os.environ["ASSET_NAME"]
rel = json.load(sys.stdin)
for a in rel.get("assets") or []:
    if a.get("name") == name:
        print(a["url"])
        sys.exit(0)
sys.exit(f"release has no asset named {name}")
'
  )"
  echo "    GET ${gh_asset} (octet-stream)"
  curl -fsSL --retry 5 --retry-delay 2 --retry-all-errors \
    -H "User-Agent: ${UA}" \
    -H "Accept: application/octet-stream" \
    "${auth_headers[@]}" \
    -L -o "$dest" \
    "$gh_asset"
}

download_analyzer_nupkg /tmp/fsharp.pureanalyzer.nupkg
unzip -qo /tmp/fsharp.pureanalyzer.nupkg "analyzers/dotnet/fs/*" -d /tmp/fspure-nupkg
cp /tmp/fspure-nupkg/analyzers/dotnet/fs/*.dll "${PREFIX}/share/fspure/analyzers/dotnet/fs/"

skill_url="https://raw.githubusercontent.com/e-St/fspure/${FSPURE_SKILL_REF}/plugins/fspure/skills/fspure-reduce-impurity/SKILL.md"
echo "==> skill ${FSPURE_SKILL_REF}"
echo "    GET ${skill_url}"
curl -fsSL --retry 3 --retry-delay 2 -H "User-Agent: ${UA}" \
  -o "${PREFIX}/share/fspure/skills/fspure-reduce-impurity/SKILL.md" \
  "$skill_url"

echo "==> fsharp-pure-decorations Open VSX"
python3 -c "import json,urllib.request; req=urllib.request.Request('https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest', headers={'User-Agent':'${UA}'}); url=json.load(urllib.request.urlopen(req))['files']['download']; urllib.request.urlretrieve(url, '${PREFIX}/share/fspure/fsharp-pure-decorations.vsix')"

rm -rf /tmp/fsharp.pureanalyzer.nupkg /tmp/fspure-nupkg
echo "==> bake done"
