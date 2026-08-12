#!/usr/bin/env bash
# Convenience hook. The CLI is the contract — see src/docs/AGENT.md.
set -euo pipefail

PROJECT="${FSPURE_PROJECT:-}"
if [[ -z "$PROJECT" ]]; then
  PROJECT="$(ls ./*.fsproj 2>/dev/null | head -1 || true)"
fi
if [[ -z "$PROJECT" ]]; then
  echo "pre-commit-fspure-analyze: set FSPURE_PROJECT or run from a directory with an .fsproj" >&2
  exit 2
fi

FOCUS=( )
if [[ -n "${FSPURE_FOCUS:-}" ]]; then
  # shellcheck disable=SC2206
  FOCUS=( --focus $FSPURE_FOCUS )
fi

IGNORE=( )
if [[ -n "${FSPURE_IGNORE:-}" ]]; then
  # shellcheck disable=SC2206
  IGNORE=( --ignore $FSPURE_IGNORE )
fi

if command -v fspure >/dev/null 2>&1; then
  exec fspure analyze --project "$PROJECT" --format json --fail-on-impure "${FOCUS[@]}" "${IGNORE[@]}"
fi

if [[ -f src/fspure/fspure.fsproj ]]; then
  exec dotnet run --project src/fspure -c Release -- analyze --project "$PROJECT" --format json --fail-on-impure "${FOCUS[@]}" "${IGNORE[@]}"
fi

echo "pre-commit-fspure-analyze: fspure not installed" >&2
exit 2
