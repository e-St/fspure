#!/usr/bin/env bash
# Install the published fspure Copilot skill into the user profile.
# User scope so it is not written into the repository.
#
# Non-interactive: --agent is required (otherwise gh prompts and Codespaces cancel).
# --pin is required: gh skill otherwise uses the latest GitHub Release tag, and
# v0.4.0 does not contain the skill. See FSPURE_SKILL_REF in versions.env.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=install-github-cli.sh
source "$HERE/install-github-cli.sh"
ensure_github_cli || true

CALLER_SKILL_REF="${FSPURE_SKILL_REF:-}"
VERSIONS="$HERE/../scripts/integrations/fstarter/versions.env"
if [[ -f "$VERSIONS" ]]; then
  # shellcheck disable=SC1090
  set -a
  # shellcheck source=/dev/null
  source "$VERSIONS"
  set +a
fi
FSPURE_SKILL_REF="${CALLER_SKILL_REF:-${FSPURE_SKILL_REF:-main}}"

skill_skip() {
  echo "WARNING: $*" >&2
  if [[ "${CI:-}" == "true" || "${FSPURE_SKILL_STRICT:-}" == "1" ]]; then
    exit 1
  fi
  exit 0
}

if ! command -v gh >/dev/null 2>&1; then
  skill_skip "gh not on PATH; skip fspure Copilot skill."
fi

if ! gh skill --help >/dev/null 2>&1; then
  skill_skip "gh skill is unavailable (need GitHub CLI 2.90+); skip fspure Copilot skill."
fi

# Fail instead of hanging if gh grows another prompt.
export GH_PROMPT_DISABLED=1
export GIT_TERMINAL_PROMPT=0

if gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin "$FSPURE_SKILL_REF" \
  --force \
  --agent github-copilot; then
  echo "✅ Copilot skill fspure-reduce-impurity (user scope, pin ${FSPURE_SKILL_REF})"
  exit 0
fi

if gh skill update fspure-reduce-impurity --agent github-copilot; then
  echo "✅ Updated Copilot skill fspure-reduce-impurity"
  exit 0
fi

echo "WARNING: could not install e-St/fspure fspure-reduce-impurity (gh auth / network / ref ${FSPURE_SKILL_REF}?)." >&2
if [[ "${CI:-}" == "true" || "${FSPURE_SKILL_STRICT:-}" == "1" ]]; then
  exit 1
fi
exit 0
