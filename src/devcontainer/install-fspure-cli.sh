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

ensure_fspure_cli() {
  ensure_local_bin_on_path
  if command -v fspure >/dev/null 2>&1 && fspure analyze --help >/dev/null 2>&1; then
    echo "✅ fspure CLI $(command -v fspure)"
    return 0
  fi

  case "$(uname -m)" in
    x86_64 | amd64) ;;
    *)
      echo "WARNING: no standalone fspure binary for $(uname -m); skip CLI install." >&2
      return 1
      ;;
  esac

  local tag="${FSPURE_CLI_RELEASE:-fspure-latest}"
  local dest="${HOME}/.local/bin/fspure"
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
