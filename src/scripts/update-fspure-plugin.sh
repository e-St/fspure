#!/usr/bin/env bash
# Keep the Claude marketplace catalog aligned with plugins/fspure.
#
# Canonical skill: plugins/fspure/skills/fspure-reduce-impurity/SKILL.md
# Copilot install (non-interactive; pin a ref that contains the skill):
#   gh skill install e-St/fspure fspure-reduce-impurity \
#     --scope user --pin main --force --agent github-copilot
#
# Usage:
#   bash src/scripts/update-fspure-plugin.sh --check
#   bash src/scripts/update-fspure-plugin.sh --sync
#   bash src/scripts/update-fspure-plugin.sh --bump patch|minor|major
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

PLUGIN_JSON="plugins/fspure/.claude-plugin/plugin.json"
MARKETPLACE=".claude-plugin/marketplace.json"

MODE="sync"
BUMP=""

die() { echo "ERROR: $*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --check) MODE="check"; shift ;;
    --sync) MODE="sync"; shift ;;
    --bump)
      MODE="bump"
      BUMP="${2:-}"
      [[ "$BUMP" == "patch" || "$BUMP" == "minor" || "$BUMP" == "major" ]] \
        || die "--bump needs patch, minor, or major"
      shift 2
      ;;
    -h | --help)
      sed -n '2,12p' "$0"
      exit 0
      ;;
    *) die "unknown arg: $1" ;;
  esac
done

[[ -f "$PLUGIN_JSON" ]] || die "missing $PLUGIN_JSON"
[[ -f "$MARKETPLACE" ]] || die "missing $MARKETPLACE"

python3 - "$MODE" "$BUMP" "$PLUGIN_JSON" "$MARKETPLACE" <<'PY'
import json, sys
from pathlib import Path

mode, bump, plugin_path, market_path = sys.argv[1:5]
plugin = json.loads(Path(plugin_path).read_text())
market = json.loads(Path(market_path).read_text())

def bump_semver(v: str, kind: str) -> str:
    parts = [int(p) for p in v.split(".")]
    while len(parts) < 3:
        parts.append(0)
    major, minor, patch = parts[0], parts[1], parts[2]
    if kind == "major":
        major, minor, patch = major + 1, 0, 0
    elif kind == "minor":
        minor, patch = minor + 1, 0
    else:
        patch += 1
    return f"{major}.{minor}.{patch}"

changed = False
errors = []

version = plugin.get("version", "0.0.0")
if mode == "bump" and bump:
    version = bump_semver(version, bump)
    plugin["version"] = version
    Path(plugin_path).write_text(json.dumps(plugin, indent=2) + "\n")
    changed = True
    print(f"plugin version → {version}")

entry = None
for p in market.get("plugins", []):
    if p.get("name") == plugin.get("name"):
        entry = p
        break
if entry is None:
    errors.append(f"{market_path}: no plugin named {plugin.get('name')!r}")
else:
    want = {
        "description": plugin.get("description", entry.get("description")),
        "version": plugin.get("version", version),
        "homepage": plugin.get("homepage", entry.get("homepage")),
    }
    for k, v in want.items():
        if v is None:
            continue
        if entry.get(k) != v:
            if mode == "check":
                errors.append(f"{market_path}: {k} is {entry.get(k)!r}, expected {v!r}")
            else:
                entry[k] = v
                changed = True
    if mode != "check" and changed:
        Path(market_path).write_text(json.dumps(market, indent=2) + "\n")
        print(f"synced {market_path}")

skill = Path("plugins/fspure/skills/fspure-reduce-impurity/SKILL.md")
if not skill.is_file():
    errors.append(f"missing canonical skill {skill}")

required_flags = ("--agent github-copilot", "--scope user", "--pin")
for rel in (
    "src/devcontainer/install-fspure-skill.sh",
    "src/scripts/integrations/fstarter/overlay/.devcontainer/setup-fspure.sh",
):
    text = Path(rel).read_text()
    for flag in required_flags:
        if flag not in text:
            errors.append(f"{rel}: missing {flag!r} (needed for non-interactive gh skill install)")

if errors:
    for e in errors:
        print(e, file=sys.stderr)
    sys.exit(1)

if mode == "check":
    print("marketplace catalog matches plugins/fspure")
    print("Copilot install scripts pass --agent github-copilot and --pin")
    sys.exit(0)

if not changed and mode != "bump":
    print("already in sync")
PY
