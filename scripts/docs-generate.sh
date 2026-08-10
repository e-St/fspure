#!/usr/bin/env bash
# Generate fspure Markdown / static site via F# + Scriban (src/DocsGenerator).
#
# Usage (repo root):
#   bash scripts/docs-generate.sh preview [ref]
#   bash scripts/docs-generate.sh stable [version]
#
# preview  → writes _site/preview/<ref>/ only (never rewrites main-branch Markdown)
# stable   → writes README.md + docs/*.md into the repo AND _site/ (release only)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODE="${1:-preview}"
REF_OR_VER="${2:-}"
CONFIGURATION="${CONFIGURATION:-Release}"

dotnet build src/DocsGenerator/DocsGenerator.fsproj -c "$CONFIGURATION" --nologo -v q

case "$MODE" in
  preview)
    REF="${REF_OR_VER:-$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo local)}"
    # Sanitize for URL path segments
    SAFE="$(echo "$REF" | tr '/ ' '--' | tr -cd 'A-Za-z0-9._-')"
    SITE="_site/preview/${SAFE}"
    BASE="https://fspure.net/preview/${SAFE}"
    echo "==> preview docs for ref=$REF → $SITE"
    dotnet run --project src/DocsGenerator -c "$CONFIGURATION" --no-build -- \
      --root "$ROOT" \
      --channel preview \
      --ref "$REF" \
      --site-out "$SITE" \
      --base-url "$BASE"
    ;;
  stable)
    VER="${REF_OR_VER:-}"
    if [[ -z "$VER" ]]; then
      VER="$(python3 -c "import json; print(json.load(open('docs/releases/manifest.json'))['lastOfficial']['FSharp.PureAnalyzer'])" 2>/dev/null || echo "0.0.0")"
    fi
    echo "==> stable docs version=$VER (writes repo Markdown + _site/)"
    dotnet run --project src/DocsGenerator -c "$CONFIGURATION" --no-build -- \
      --root "$ROOT" \
      --channel stable \
      --ref "v${VER}" \
      --version "$VER" \
      --write-repo-files \
      --site-out "_site" \
      --base-url "https://fspure.net"
    ;;
  *)
    echo "Usage: $0 preview [ref] | stable [version]" >&2
    exit 2
    ;;
esac
