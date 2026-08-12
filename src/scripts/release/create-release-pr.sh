#!/usr/bin/env bash
# Create/update the release PR from src/docs/releases/ pending state.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

DATE="$(date -u +%Y%m%d)"
BRANCH="${RELEASE_PR_BRANCH:-release/prepare-${DATE}}"
TITLE="${RELEASE_PR_TITLE:-chore(release): prepare official release}"
git checkout -B "$BRANCH"
git add src/docs/releases/
if git diff --staged --quiet; then
  echo "No release file changes"
  exit 0
fi

BODY="$(python3 - <<'PY'
import json
m = json.load(open("src/docs/releases/manifest.json"))
p = m["pending"]
lines = [
  "## Release PR",
  "",
  "Edit **versions** and **`publish` flags** in `src/docs/releases/manifest.json`, and rewrite each **Unreleased** section in the changelogs before merge.",
  "",
  "### Proposed",
  "",
  "| Component | From | To | Publish |",
  "|-----------|------|----|---------|",
]
for name, c in p["components"].items():
    lines.append(f"| `{name}` | {c['from']} | **{c['to']}** | {c['publish']} |")
lines += ["", "### Changelogs", ""]
for name, c in p["components"].items():
    clog = c.get("changelog", "")
    if clog:
        lines.append(f"- [{clog.rsplit('/', 1)[-1]}]({clog})")
lines += [
  "",
  "### After merge",
  "",
  "The **Official release** workflow will:",
  "1. Publish selected packages / skill tag (nuget.org + GitHub Releases)",
  "2. Update version pins in the monorepo (README, sample, fstarter pack, `FSPURE_SKILL_REF`)",
  "3. Trigger ready-lib sync and fstarter PR workflows",
  "",
  "Docs: [src/docs/RELEASING.md](src/docs/RELEASING.md)",
]
print("\n".join(lines))
PY
)"

git commit -m "chore(release): prepare official release PR

Editable versions and changelogs under src/docs/releases/. Merge to publish."

git push -u origin "HEAD:${BRANCH}" --force

REPO="${GITHUB_REPOSITORY:-e-St/fspure}"
COMPARE="https://github.com/${REPO}/compare/main...${BRANCH}?expand=1"

fail_pr() {
  echo "ERROR: could not create or update the Release PR." >&2
  echo "Branch is on origin: ${BRANCH}" >&2
  echo "Open it: ${COMPARE}" >&2
  echo >&2
  echo "The default GITHUB_TOKEN cannot create PRs unless the repo (or org) has" >&2
  echo "  Settings → Actions → General → Workflow permissions →" >&2
  echo "  Allow GitHub Actions to create and approve pull requests" >&2
  echo "Or set secret FSPURE_RELEASE_PR_TOKEN (fine-grained PAT on this repo:" >&2
  echo "  Contents read/write + Pull requests read/write)." >&2
  exit 1
}

EXISTING="$(gh pr list --head "$BRANCH" --json number --jq '.[0].number // empty')"
if [[ -n "$EXISTING" ]]; then
  gh pr edit "$EXISTING" --title "$TITLE" --body "$BODY" || fail_pr
  gh pr edit "$EXISTING" --add-label "release" 2>/dev/null || true
  echo "Updated PR #$EXISTING"
  exit 0
fi

if ! gh pr create --base main --head "$BRANCH" --title "$TITLE" --body "$BODY"; then
  fail_pr
fi
NUM="$(gh pr list --head "$BRANCH" --json number --jq '.[0].number // empty')"
if [[ -z "$NUM" ]]; then
  fail_pr
fi
gh pr edit "$NUM" --add-label "release" 2>/dev/null || \
  gh label create release --description "Official release PR" --color "0E8A16" 2>/dev/null || true
gh pr edit "$NUM" --add-label "release" 2>/dev/null || true
echo "Created PR #$NUM"
