#!/usr/bin/env bash
# Install GitHub CLI (gh) if missing or too old for `gh skill` (2.90+).
# Safe to source: defines ensure_github_cli. Also runnable as a script.
ensure_github_cli() {
  if command -v gh >/dev/null 2>&1 && gh skill --help >/dev/null 2>&1; then
    return 0
  fi

  echo "==> Installing GitHub CLI (gh skill needs 2.90+)"

  if ! command -v sudo >/dev/null 2>&1 && [[ "$(id -u)" -ne 0 ]]; then
    echo "WARNING: cannot install gh (no sudo)." >&2
    return 1
  fi

  local run=""
  if [[ "$(id -u)" -ne 0 ]]; then
    run="sudo"
  fi

  local arch
  case "$(uname -m)" in
    x86_64 | amd64) arch=amd64 ;;
    aarch64 | arm64) arch=arm64 ;;
    *)
      echo "WARNING: unsupported architecture $(uname -m) for gh .deb." >&2
      return 1
      ;;
  esac

  local ver
  ver="$(
    curl -fsSL https://api.github.com/repos/cli/cli/releases/latest \
      | python3 -c "import json,sys; print(json.load(sys.stdin)['tag_name'].lstrip('v'))"
  )" || {
    echo "WARNING: could not resolve latest gh release." >&2
    return 1
  }

  local deb
  deb="$(mktemp --suffix=.deb)"
  # shellcheck disable=SC2064
  trap "rm -f '$deb'" RETURN

  if ! curl -fsSL -o "$deb" "https://github.com/cli/cli/releases/download/v${ver}/gh_${ver}_linux_${arch}.deb"; then
    echo "WARNING: failed to download gh ${ver}." >&2
    return 1
  fi

  if ! $run dpkg -i "$deb"; then
    $run apt-get update -qq
    $run apt-get install -y -f -qq
  fi

  if command -v gh >/dev/null 2>&1 && gh skill --help >/dev/null 2>&1; then
    echo "✅ GitHub CLI $(gh --version | head -1)"
    return 0
  fi

  echo "WARNING: gh installed but 'gh skill' is still missing." >&2
  return 1
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  set -euo pipefail
  ensure_github_cli
fi
