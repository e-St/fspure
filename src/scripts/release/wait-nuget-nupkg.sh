#!/usr/bin/env bash
# Block until nuget.org's flat container actually serves a nupkg.
#
# `dotnet nuget push` returns when the upload is accepted. Listing on
# api.nuget.org/v3-flatcontainer can lag several minutes. fstarter's image
# bake curls that URL and fails the devcontainer build on 404 — and curl's
# --retry does not retry HTTP 404. Do not open an fstarter PR until this
# succeeds.
#
# Usage: wait-nuget-nupkg.sh <package-id> <version> [timeout-seconds]
set -euo pipefail

ID="${1:?package id}"
VER="${2:?version}"
TIMEOUT="${3:-${NUGET_WAIT_TIMEOUT:-1200}}"
INTERVAL="${NUGET_WAIT_INTERVAL:-20}"

if [[ "$VER" == "latest" || -z "$VER" ]]; then
  echo "ERROR: refusing to wait for nuget.org version '${VER}'" >&2
  exit 1
fi

id_lc="$(printf '%s' "$ID" | tr '[:upper:]' '[:lower:]')"
url="https://api.nuget.org/v3-flatcontainer/${id_lc}/${VER}/${id_lc}.${VER}.nupkg"

probe() {
  local code
  code="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 30 -I -L "$url" || echo "000")"
  if [[ "$code" == "200" || "$code" == "206" ]]; then
    return 0
  fi
  code="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 30 -L -r 0-0 "$url" || echo "000")"
  [[ "$code" == "200" || "$code" == "206" ]]
}

echo "==> wait for nuget.org ${ID} ${VER}"
echo "    GET ${url}"
echo "    timeout=${TIMEOUT}s interval=${INTERVAL}s"
deadline=$((SECONDS + TIMEOUT))
while true; do
  if probe; then
    echo "nuget.org is serving ${ID} ${VER}"
    exit 0
  fi
  if (( SECONDS >= deadline )); then
    echo "ERROR: ${url} was not downloadable after ${TIMEOUT}s." >&2
    echo "Refusing to continue: fstarter image bake fails CI if this URL 404s." >&2
    exit 1
  fi
  echo "    not listed yet; retrying in ${INTERVAL}s"
  sleep "$INTERVAL"
done
