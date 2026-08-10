#!/usr/bin/env bash
# Shared helpers for release scripts (source this file; do not execute alone).
# shellcheck shell=bash

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MANIFEST="${MANIFEST:-$ROOT/releases/manifest.json}"

die() { echo "ERROR: $*" >&2; exit 1; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "missing command: $1"
}

semver_bump_patch() {
  local v="$1"
  python3 - <<PY
v = "$v".split(".")
while len(v) < 3:
    v.append("0")
major, minor, patch = int(v[0]), int(v[1]), int(v[2].split("-")[0])
print(f"{major}.{minor}.{patch + 1}")
PY
}

git_log_since_tag() {
  local path="$1"
  local version="$2"
  local tag="v${version}"
  if git rev-parse "$tag" >/dev/null 2>&1; then
    git -C "$ROOT" log --no-merges --pretty=format:'- %s (%h)' "${tag}..HEAD" -- "$path" || true
  elif git rev-parse "$version" >/dev/null 2>&1; then
    git -C "$ROOT" log --no-merges --pretty=format:'- %s (%h)' "${version}..HEAD" -- "$path" || true
  else
    git -C "$ROOT" log --no-merges --pretty=format:'- %s (%h)' -n 30 -- "$path" || true
  fi
  echo
}
