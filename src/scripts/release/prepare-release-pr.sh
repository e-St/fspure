#!/usr/bin/env bash
# Build src/docs/releases/manifest.json pending block + draft Unreleased changelog sections.
# Used by workflow "Prepare release PR" (does not publish).
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
require_cmd python3
require_cmd git

cd "$ROOT"

ANALYZER_TO="${ANALYZER_TO:-}"
COLLECTOR_TO="${COLLECTOR_TO:-}"
EXTENSION_TO="${EXTENSION_TO:-}"
SKILL_TO="${SKILL_TO:-}"
PUBLISH_ANALYZER="${PUBLISH_ANALYZER:-true}"
PUBLISH_COLLECTOR="${PUBLISH_COLLECTOR:-true}"
PUBLISH_EXTENSION="${PUBLISH_EXTENSION:-false}"
PUBLISH_SKILL="${PUBLISH_SKILL:-true}"

export MANIFEST
python3 - <<'PY'
import json, os, subprocess, pathlib, datetime, re

root = pathlib.Path(".").resolve()
manifest_path = pathlib.Path(os.environ.get("MANIFEST") or (root / "src/docs/releases/manifest.json"))
m = json.loads(manifest_path.read_text())
last = m["lastOfficial"]

def bump(v: str) -> str:
    parts = v.split(".")
    while len(parts) < 3:
        parts.append("0")
    major, minor, patch = int(parts[0]), int(parts[1]), int(parts[2].split("-")[0])
    return f"{major}.{minor}.{patch + 1}"

def git_log(path: str, version: str, extra_tags=()) -> str:
    candidates = [f"v{version}", version, *extra_tags]
    for tag in candidates:
        r = subprocess.run(
            ["git", "rev-parse", tag],
            cwd=root,
            capture_output=True,
            text=True,
        )
        if r.returncode == 0:
            r2 = subprocess.run(
                ["git", "log", "--no-merges", "--pretty=format:- %s (%h)", f"{tag}..HEAD", "--", path],
                cwd=root,
                capture_output=True,
                text=True,
            )
            return (r2.stdout or "").strip()
    r2 = subprocess.run(
        ["git", "log", "--no-merges", "--pretty=format:- %s (%h)", "-n", "30", "--", path],
        cwd=root,
        capture_output=True,
        text=True,
    )
    return (r2.stdout or "").strip()

def env_or(name: str, default: str) -> str:
    v = os.environ.get(name, "").strip()
    return v if v else default

def last_or(name: str, default: str) -> str:
    v = last.get(name)
    return v if v else default

skill_from = last_or("fspure-reduce-impurity", "0.1.0")

pending = {
    "preparedAtUtc": datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"),
    "preparedBy": os.environ.get("GITHUB_ACTOR", "local"),
    "instructions": (
        "Edit each component's `to` version and `publish` flag. "
        "Edit the matching CHANGELOG.* Unreleased section. "
        "Merge this PR to trigger official publish."
    ),
    "components": {
        "FSharp.PureAnalyzer": {
            "from": last["FSharp.PureAnalyzer"],
            "to": env_or("ANALYZER_TO", bump(last["FSharp.PureAnalyzer"])),
            "publish": os.environ.get("PUBLISH_ANALYZER", "true").lower() == "true",
            "paths": ["src/FSharp.PureAnalyzer/", "src/FSharp.PureSchema/", "src/Fspure.Embed/"],
            "changelog": "src/docs/releases/CHANGELOG.FSharp.PureAnalyzer.md",
        },
        "fspure-collector": {
            "from": last["fspure-collector"],
            "to": env_or("COLLECTOR_TO", bump(last["fspure-collector"])),
            "publish": os.environ.get("PUBLISH_COLLECTOR", "true").lower() == "true",
            "paths": ["src/fspure-collector/", "src/FSharp.PureSchema/"],
            "changelog": "src/docs/releases/CHANGELOG.fspure-collector.md",
        },
        "fsharp-pure-decorations": {
            "from": last["fsharp-pure-decorations"],
            "to": env_or("EXTENSION_TO", bump(last["fsharp-pure-decorations"])),
            "publish": os.environ.get("PUBLISH_EXTENSION", "false").lower() == "true",
            "paths": ["src/editor/vscode-extension/"],
            "changelog": "src/docs/releases/CHANGELOG.fsharp-pure-decorations.md",
        },
        "fspure-reduce-impurity": {
            "from": skill_from,
            "to": env_or("SKILL_TO", bump(skill_from)),
            "publish": os.environ.get("PUBLISH_SKILL", "true").lower() == "true",
            "paths": ["plugins/fspure/"],
            "changelog": "src/docs/releases/CHANGELOG.fspure-reduce-impurity.md",
            "tag": f"fspure-reduce-impurity-v{env_or('SKILL_TO', bump(skill_from))}",
        },
    },
}

m["pending"] = pending
manifest_path.write_text(json.dumps(m, indent=2) + "\n")
print("Wrote pending release to", manifest_path)

path_map = {
    "FSharp.PureAnalyzer": (
        "src/docs/releases/CHANGELOG.FSharp.PureAnalyzer.md",
        "src/FSharp.PureAnalyzer/",
        last["FSharp.PureAnalyzer"],
        (),
    ),
    "fspure-collector": (
        "src/docs/releases/CHANGELOG.fspure-collector.md",
        "src/fspure-collector/",
        last["fspure-collector"],
        (f"fspure-collector-v{last['fspure-collector']}",),
    ),
    "fsharp-pure-decorations": (
        "src/docs/releases/CHANGELOG.fsharp-pure-decorations.md",
        "src/editor/vscode-extension/",
        last["fsharp-pure-decorations"],
        (f"vscode-extension-v{last['fsharp-pure-decorations']}",),
    ),
    "fspure-reduce-impurity": (
        "src/docs/releases/CHANGELOG.fspure-reduce-impurity.md",
        "plugins/fspure/",
        skill_from,
        (f"fspure-reduce-impurity-v{skill_from}",),
    ),
}

for name, (clog, path, from_ver, extra) in path_map.items():
    if not pending["components"][name].get("publish"):
        print("Skip Unreleased draft for unpublished", name)
        continue
    clog_path = root / clog
    text = clog_path.read_text()
    commits = git_log(path, from_ver, extra)
    if not commits:
        commits = "- _(no path-specific commits found since last tag — edit this section)_"
    draft = f"## [Unreleased]\n\n### Draft (from git log — edit freely)\n\n{commits}\n\n"
    if re.search(r"^## \[Unreleased\]", text, re.M):
        text = re.sub(
            r"^## \[Unreleased\][\s\S]*?(?=^## \[|\Z)",
            draft,
            text,
            count=1,
            flags=re.M,
        )
    else:
        text = draft + "\n" + text
    clog_path.write_text(text)
    print("Updated Unreleased draft in", clog)
PY

echo ""
echo "Pending release prepared. Review src/docs/releases/manifest.json and CHANGELOG.*.md"
python3 -m json.tool "$MANIFEST" | head -100
