#!/usr/bin/env bash
# Publish official packages from releases/manifest.json pending block.
# Requires: NUGET_API_KEY or NuGet/login already done via env NUGET_API_KEY;
#            GH_TOKEN for GitHub Release; optional OVSX_PAT for extension.
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
require_cmd python3
require_cmd dotnet
require_cmd git

cd "$ROOT"

python3 - <<'PY'
import json, pathlib, sys
m = json.loads(pathlib.Path("releases/manifest.json").read_text())
if not m.get("pending"):
    print("ERROR: releases/manifest.json has no pending release", file=sys.stderr)
    sys.exit(1)
comps = m["pending"]["components"]
for name, c in comps.items():
    if c.get("publish"):
        print(f"WILL_PUBLISH {name} {c['from']} -> {c['to']}")
    else:
        print(f"SKIP {name} (publish=false) stay {c['from']}")
PY

export CONFIGURATION="${CONFIGURATION:-Release}"

publish_analyzer() {
  local ver="$1"
  echo "==> Pack FSharp.PureAnalyzer $ver"
  (
    cd FSharp.PureAnalyzer
    if command -v paket >/dev/null 2>&1; then paket restore
    elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then "$HOME/.dotnet/tools/paket" restore
    fi
    dotnet pack FSharp.PureAnalyzer.fsproj \
      -c "$CONFIGURATION" \
      -o ./nupkgs \
      "/p:Version=$ver" \
      "/p:PackageVersion=$ver" \
      --nologo
  )
  local nupkg="FSharp.PureAnalyzer/nupkgs/FSharp.PureAnalyzer.${ver}.nupkg"
  [[ -f "$nupkg" ]] || die "missing $nupkg"
  if [[ -n "${NUGET_API_KEY:-}" ]]; then
    dotnet nuget push "$nupkg" --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
  else
    echo "WARN: NUGET_API_KEY unset — skip nuget.org push for analyzer"
  fi
  if [[ -n "${GH_TOKEN:-}" ]]; then
    local tag="v${ver}"
    if ! gh release view "$tag" >/dev/null 2>&1; then
      gh release create "$tag" --title "$ver" --generate-notes --latest || \
        gh release create "$tag" --title "$ver" --notes "FSharp.PureAnalyzer $ver" --latest
    else
      gh release edit "$tag" --latest || true
    fi
    gh release upload "$tag" "$nupkg" --clobber
  fi
  if [[ -n "${GITHUB_TOKEN:-${GH_TOKEN:-}}" ]]; then
    local token="${GITHUB_TOKEN:-$GH_TOKEN}"
    local owner="${GITHUB_REPOSITORY_OWNER:-e-St}"
    dotnet nuget push "$nupkg" --api-key "$token" \
      --source "https://nuget.pkg.github.com/${owner}/index.json" --skip-duplicate || true
  fi
}

publish_collector() {
  local ver="$1"
  echo "==> Pack fspure-collector $ver"
  (
    cd fspure-collector
    if command -v paket >/dev/null 2>&1; then paket restore
    elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then "$HOME/.dotnet/tools/paket" restore
    fi
    dotnet pack fspure-collector.fsproj \
      -c "$CONFIGURATION" \
      -o ./nupkgs \
      "/p:Version=$ver" \
      "/p:PackageVersion=$ver" \
      --nologo
  )
  local nupkg="fspure-collector/nupkgs/fspure-collector.${ver}.nupkg"
  [[ -f "$nupkg" ]] || die "missing $nupkg"
  if [[ -n "${NUGET_API_KEY:-}" ]]; then
    dotnet nuget push "$nupkg" --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
  fi
  if [[ -n "${GH_TOKEN:-}" ]]; then
    # Attach to analyzer release tag if that was published; else own tag
    local tag="${ANALYZER_RELEASE_TAG:-v${ver}}"
    if gh release view "$tag" >/dev/null 2>&1; then
      gh release upload "$tag" "$nupkg" --clobber || true
    else
      local ctag="fspure-collector-v${ver}"
      if ! gh release view "$ctag" >/dev/null 2>&1; then
        gh release create "$ctag" --title "fspure-collector $ver" --notes "fspure-collector $ver" "$nupkg" || true
      else
        gh release upload "$ctag" "$nupkg" --clobber || true
      fi
    fi
  fi
}

publish_extension() {
  local ver="$1"
  echo "==> Package vscode-extension $ver"
  (
    cd vscode-extension
    python3 - <<PY
import json, pathlib
p = pathlib.Path("package.json")
d = json.loads(p.read_text())
d["version"] = "$ver"
p.write_text(json.dumps(d, indent=2) + "\n")
PY
    npx --yes @vscode/vsce package --allow-missing-repository -o "fsharp-pure-decorations-${ver}.vsix"
  )
  local vsix="vscode-extension/fsharp-pure-decorations-${ver}.vsix"
  [[ -f "$vsix" ]] || die "missing $vsix"
  if [[ -n "${GH_TOKEN:-}" ]]; then
    local tag="vscode-extension-v${ver}"
    if ! gh release view "$tag" >/dev/null 2>&1; then
      gh release create "$tag" --title "VS Code extension $ver" --notes "fsharp-pure-decorations $ver" "$vsix"
    else
      gh release upload "$tag" "$vsix" --clobber
    fi
  fi
  if [[ -n "${OVSX_PAT:-}" ]]; then
    npx --yes ovsx publish "$vsix" -p "$OVSX_PAT" || echo "WARN: ovsx publish failed"
  else
    echo "WARN: OVSX_PAT unset — skip Open VSX publish"
  fi
}

# Finalize changelogs: move Unreleased draft under new version heading
finalize_changelog() {
  local file="$1"
  local ver="$2"
  local date
  date="$(date -u +%Y-%m-%d)"
  python3 - <<PY
import pathlib, re
path = pathlib.Path("$file")
text = path.read_text()
ver, date = "$ver", "$date"
# Replace ## [Unreleased] block header with versioned section + empty Unreleased
if re.search(r"^## \[Unreleased\]", text, re.M):
    text = re.sub(
        r"^## \[Unreleased\]\s*",
        f"## [Unreleased]\n\n## [{ver}] — {date}\n\n",
        text,
        count=1,
        flags=re.M,
    )
    path.write_text(text)
    print("finalized changelog", path, ver)
else:
    print("no Unreleased section in", path)
PY
}

mapfile -t LINES < <(python3 - <<'PY'
import json
m = json.load(open("releases/manifest.json"))
for name, c in m["pending"]["components"].items():
    pub = "1" if c.get("publish") else "0"
    print(f"{name}|{c['to']}|{pub}|{c['changelog']}")
PY
)

ANALYZER_RELEASE_TAG=""
for line in "${LINES[@]}"; do
  IFS='|' read -r name to pub clog <<<"$line"
  if [[ "$pub" != "1" ]]; then
    continue
  fi
  case "$name" in
    FSharp.PureAnalyzer)
      publish_analyzer "$to"
      ANALYZER_RELEASE_TAG="v${to}"
      export ANALYZER_RELEASE_TAG
      finalize_changelog "$clog" "$to"
      ;;
    fspure-collector)
      publish_collector "$to"
      finalize_changelog "$clog" "$to"
      ;;
    fsharp-pure-decorations)
      publish_extension "$to"
      finalize_changelog "$clog" "$to"
      ;;
    *)
      die "unknown component $name"
      ;;
  esac
done

# Promote pending → lastOfficial, clear pending
python3 - <<'PY'
import json, pathlib
path = pathlib.Path("releases/manifest.json")
m = json.loads(path.read_text())
pending = m["pending"]
for name, c in pending["components"].items():
    if c.get("publish"):
        m["lastOfficial"][name] = c["to"]
m["pending"] = None
path.write_text(json.dumps(m, indent=2) + "\n")
print("manifest lastOfficial:", m["lastOfficial"])
PY

bash "$ROOT/scripts/release/apply-version-pins.sh"

echo "Official publish complete."
