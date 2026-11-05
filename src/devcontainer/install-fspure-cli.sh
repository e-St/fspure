#!/usr/bin/env bash
# Put the standalone `fspure` CLI on PATH (~/.local/bin).
# Safe to source: defines ensure_fspure_cli. Also runnable as a script.
#
# The linux-x64 binary is the `fspure-latest` GitHub Release (updated on main).
# Do not apt-install or `gh release download` from agent sessions — setup does this.
ensure_local_bin_on_path() {
  mkdir -p "${HOME}/.local/bin"
  case ":${PATH}:" in
    *":${HOME}/.local/bin:"*) ;;
    *) export PATH="${HOME}/.local/bin:${PATH}" ;;
  esac
  local line='export PATH="$HOME/.local/bin:$PATH"'
  for rc in "${HOME}/.bashrc" "${HOME}/.profile"; do
    if [[ -f "$rc" ]] && grep -qxF "$line" "$rc" 2>/dev/null; then
      continue
    fi
    printf '\n%s\n' "$line" >>"$rc"
  done
}

cli_release_size() {
  local tag="$1"
  curl -fsSL --retry 2 --retry-delay 1 \
    -H "Accept: application/vnd.github+json" \
    -H "User-Agent: fspure-setup" \
    "https://api.github.com/repos/e-St/fspure/releases/tags/${tag}" \
    | python3 -c 'import json,sys
rel=json.load(sys.stdin)
for a in rel.get("assets") or []:
    if a.get("name") == "fspure":
        print(a.get("size") or "")
        raise SystemExit(0)
raise SystemExit(1)
'
}

# True when the binary works and matches the published release asset.
# If the release cannot be queried, a working binary is kept.
cli_matches_release() {
  local bin="$1" tag="$2" remote size
  [[ -x "$bin" ]] || return 1
  "$bin" analyze --help >/dev/null 2>&1 || return 1
  remote="$(cli_release_size "$tag" 2>/dev/null || true)"
  [[ -n "$remote" ]] || return 0
  size="$(stat -c %s "$bin" 2>/dev/null || echo 0)"
  [[ "$size" == "$remote" ]]
}

ensure_fspure_cli() {
  ensure_local_bin_on_path
  local caller_tag="${FSPURE_CLI_RELEASE:-}"
  local versions
  versions="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/../scripts/integrations/fstarter/versions.env"
  if [[ -f "$versions" ]]; then
    # shellcheck disable=SC1090
    set -a
    # shellcheck source=/dev/null
    source "$versions"
    set +a
  fi
  local tag="${caller_tag:-${FSPURE_CLI_RELEASE:-fspure-latest}}"
  local dest="${HOME}/.local/bin/fspure"
  # ~/.local/bin is ahead of the image binary. Replace it when the release changed.
  if cli_matches_release "$dest" "$tag"; then
    echo "✅ fspure CLI ${dest} (${tag})"
    return 0
  fi
  if cli_matches_release /usr/local/bin/fspure "$tag" && [[ ! -e "$dest" ]]; then
    echo "✅ fspure CLI /usr/local/bin/fspure (${tag})"
    return 0
  fi

  case "$(uname -m)" in
    x86_64 | amd64) ;;
    *)
      echo "WARNING: no standalone fspure binary for $(uname -m); skip CLI install." >&2
      return 1
      ;;
  esac

  local url="https://github.com/e-St/fspure/releases/download/${tag}/fspure"
  echo "==> Installing fspure CLI (${tag}) → ${dest}"
  if ! curl -fsSL -o "$dest" "$url"; then
    echo "WARNING: could not download ${url}" >&2
    rm -f "$dest"
    return 1
  fi
  chmod +x "$dest"
  if ! "$dest" analyze --help >/dev/null 2>&1; then
    echo "WARNING: downloaded fspure is not usable." >&2
    return 1
  fi
  echo "✅ fspure CLI ${dest}"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  set -euo pipefail
  ensure_fspure_cli
fi
