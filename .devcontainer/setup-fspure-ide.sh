#!/usr/bin/env bash
# Install analyzer + decorations extension for the IDE experience.
# Each step prefers the public registry (nuget.org / Open VSX) and falls back
# to packaging from this repo when the package is not published yet.
#
# Skipped in GitHub Actions: devcontainers/ci runs postCreate before pack/publish,
# and those jobs do not need Ionide/extension install.
set -euo pipefail

if [[ "${SKIP_FSPURE_IDE_SETUP:-}" == "1" ]] || [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
  echo "Skipping fspure IDE setup (GITHUB_ACTIONS or SKIP_FSPURE_IDE_SETUP=1)."
  exit 0
fi

# shellcheck source=nuget-tmp-env.sh
source "$(dirname "$0")/nuget-tmp-env.sh"

cd "$(dirname "$0")"
bash install-analyzer-nuget.sh
bash install-openvsx-extension.sh
