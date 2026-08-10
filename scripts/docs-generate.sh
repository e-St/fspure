#!/usr/bin/env bash
# Generate fspure Markdown / static site via F# + Scriban (src/DocsGenerator).
#
# Usage (repo root):
#   bash scripts/docs-generate.sh preview [ref]
#   bash scripts/docs-generate.sh stable [version]
#
# preview  → _site/preview/<ref>/ only (GitHub Pages github.io URL — never fspure.net)
# stable   → README.md + docs/*.md + _site/ (for official release → fspure.net only)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODE="${1:-preview}"
REF_OR_VER="${2:-}"
CONFIGURATION="${CONFIGURATION:-Release}"

# Default GitHub Pages site for this repo (no custom domain).
GH_PAGES_BASE="${GH_PAGES_BASE:-https://e-st.github.io/fspure}"
# Custom domain — only used for stable/official release site root.
STABLE_BASE="${STABLE_BASE:-https://fspure.net}"

dotnet build src/DocsGenerator/DocsGenerator.fsproj -c "$CONFIGURATION" --nologo -v q

case "$MODE" in
  preview)
    REF="${REF_OR_VER:-$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo local)}"
    SAFE="$(echo "$REF" | tr '/ ' '--' | tr -cd 'A-Za-z0-9._-')"
    SITE="_site/preview/${SAFE}"
    # Previews are ONLY advertised on the github.io host.
    BASE="${GH_PAGES_BASE}/preview/${SAFE}"
    echo "==> preview docs for ref=$REF → $SITE"
    echo "    public URL (github.io only): $BASE"
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
    echo "==> stable docs version=$VER (repo Markdown + _site/ for fspure.net)"
    echo "    public URL (custom domain): $STABLE_BASE"
    dotnet run --project src/DocsGenerator -c "$CONFIGURATION" --no-build -- \
      --root "$ROOT" \
      --channel stable \
      --ref "v${VER}" \
      --version "$VER" \
      --write-repo-files \
      --site-out "_site" \
      --base-url "$STABLE_BASE"
    ;;
  *)
    echo "Usage: $0 preview [ref] | stable [version]" >&2
    exit 2
    ;;
esac
