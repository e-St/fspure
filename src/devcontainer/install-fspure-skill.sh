#!/usr/bin/env bash
# Install the published fspure Copilot skill into the user profile.
# User scope so it is not written into the repository.
set -euo pipefail

if ! command -v gh >/dev/null 2>&1; then
  echo "WARNING: gh not on PATH; skip fspure Copilot skill." >&2
  exit 0
fi

if ! gh skill --help >/dev/null 2>&1; then
  echo "WARNING: gh skill is unavailable (need GitHub CLI 2.90+); skip fspure Copilot skill." >&2
  exit 0
fi

if gh skill install e-St/fspure fspure-reduce-impurity --scope user; then
  echo "✅ Copilot skill fspure-reduce-impurity (user scope)"
  exit 0
fi

if gh skill update fspure-reduce-impurity; then
  echo "✅ Updated Copilot skill fspure-reduce-impurity"
  exit 0
fi

echo "WARNING: could not install e-St/fspure fspure-reduce-impurity (gh auth / network?)." >&2
exit 0
