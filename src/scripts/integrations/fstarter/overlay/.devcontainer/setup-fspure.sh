#!/usr/bin/env bash
# Install fspure into an fstarter Codespace / dev container.
# - FSharp.PureAnalyzer from nuget.org (pinned version) → workspace analyzers/dotnet/fs/
# - fsharp-pure-decorations from Open VSX VSIX (not MS Marketplace id)
# - standalone `fspure` CLI → ~/.local/bin (so Copilot does not download releases)
# - Copilot skill via non-interactive `gh skill install`
#
# Version pin: FSPURE_ANALYZER_VERSION env, or .devcontainer/fspure-versions.env
# (synced from e-St/fspure src/scripts/integrations/fstarter/versions.env).
set -euo pipefail

if [[ "${SKIP_FSPURE_SETUP:-}" == "1" ]]; then
  echo "Skipping fspure setup (SKIP_FSPURE_SETUP=1)."
  exit 0
fi

export TMPDIR="${TMPDIR:-${HOME}/.cache/nuget-tmp}"
export TEMP="${TEMP:-$TMPDIR}"
export TMP="${TMP:-$TMPDIR}"
mkdir -p "$TMPDIR"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DC_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE_ANALYZERS="$ROOT/analyzers/dotnet/fs"
GLOBAL_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
PUBLISHER_EXT="e-st.fsharp-pure-decorations"
OPENVSX_API="https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest"

# Load pin from synced versions file (preferred) or env.
if [[ -f "$DC_DIR/fspure-versions.env" ]]; then
  # shellcheck disable=SC1091
  set -a
  # shellcheck source=/dev/null
  source "$DC_DIR/fspure-versions.env"
  set +a
fi

# Default keeps templates working if the versions file is missing.
FSPURE_ANALYZER_VERSION="${FSPURE_ANALYZER_VERSION:-0.4.0}"
# Official pin is fspure-reduce-impurity-v*; main until that tag exists.
FSPURE_SKILL_REF="${FSPURE_SKILL_REF:-main}"
# Standalone linux-x64 CLI (GitHub Release tag). Not the analyzer nuget version.
FSPURE_CLI_RELEASE="${FSPURE_CLI_RELEASE:-fspure-latest}"

code_cli_usable() {
  command -v code >/dev/null 2>&1 || return 1
  local out
  if ! out="$(code --version 2>&1)"; then
    return 1
  fi
  [[ "$out" != *"not installed"* ]] || return 1
  return 0
}

mirror_from_global() {
  local src schema pkg_dir
  pkg_dir="$GLOBAL_PACKAGES/fsharp.pureanalyzer/${FSPURE_ANALYZER_VERSION}"
  src="$pkg_dir/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
  if [[ ! -f "$src" ]]; then
    # Fall back to newest installed layout (upgrade path / floating restore).
    src="$(
      find "$GLOBAL_PACKAGES/fsharp.pureanalyzer" \
        -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' 2>/dev/null \
        | sort -V \
        | tail -1 || true
    )"
  fi
  if [[ -z "${src:-}" || ! -f "$src" ]]; then
    return 1
  fi
  schema="$(dirname "$src")/FSharp.PureSchema.dll"
  mkdir -p "$WORKSPACE_ANALYZERS"
  cp -f "$src" "$WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
  if [[ -f "$schema" ]]; then
    cp -f "$schema" "$WORKSPACE_ANALYZERS/FSharp.PureSchema.dll"
  else
    echo "WARNING: FSharp.PureSchema.dll missing next to $src (older package?)" >&2
  fi
  echo "    workspace → $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll"
  if [[ -f "$WORKSPACE_ANALYZERS/FSharp.PureSchema.dll" ]]; then
    echo "    workspace → $WORKSPACE_ANALYZERS/FSharp.PureSchema.dll"
  fi
}

install_analyzer_nuget() {
  local tmp
  tmp="$(mktemp -d)"
  # shellcheck disable=SC2064
  trap "rm -rf '$tmp'" RETURN
  echo "==> FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION}: nuget.org"
  (
    cd "$tmp"
    # Quiet via redirect — `dotnet add package` does not accept --verbosity on all SDKs.
    dotnet new classlib -n install -f net10.0 --force --language C# >/dev/null
    cd install
    dotnet add package FSharp.PureAnalyzer --version "$FSPURE_ANALYZER_VERSION" >/dev/null
  )
}

install_extension_openvsx() {
  local vsix url
  vsix="$(mktemp --suffix=.vsix)"
  # shellcheck disable=SC2064
  trap "rm -f '$vsix'" RETURN
  echo "==> fsharp-pure-decorations: Open VSX VSIX"
  url="$(curl -fsSL "$OPENVSX_API" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
  curl -fsSL -o "$vsix" "$url"
  code --install-extension "$vsix" --force
}

echo "==> fspure setup (fstarter)"
echo "    analyzer pin: $FSPURE_ANALYZER_VERSION"

if install_analyzer_nuget && mirror_from_global; then
  echo "✅ FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION} installed and mirrored for Ionide"
else
  echo "ERROR: could not install or mirror FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION} from nuget.org" >&2
  exit 1
fi

if code_cli_usable; then
  if install_extension_openvsx; then
    echo "✅ Installed $PUBLISHER_EXT (Open VSX VSIX)"
  else
    echo "WARNING: Open VSX VSIX install failed." >&2
  fi
else
  echo "WARNING: VS Code 'code' CLI not usable; skip extension install." >&2
fi

ensure_github_cli() {
  if command -v gh >/dev/null 2>&1 && gh skill --help >/dev/null 2>&1; then
    return 0
  fi
  echo "==> Installing GitHub CLI (gh skill needs 2.90+)"
  local arch ver deb run=""
  case "$(uname -m)" in
    x86_64 | amd64) arch=amd64 ;;
    aarch64 | arm64) arch=arm64 ;;
    *)
      echo "WARNING: unsupported architecture $(uname -m) for gh." >&2
      return 1
      ;;
  esac
  [[ "$(id -u)" -eq 0 ]] || run="sudo"
  ver="$(
    curl -fsSL https://api.github.com/repos/cli/cli/releases/latest \
      | python3 -c "import json,sys; print(json.load(sys.stdin)['tag_name'].lstrip('v'))"
  )" || return 1
  deb="$(mktemp --suffix=.deb)"
  curl -fsSL -o "$deb" "https://github.com/cli/cli/releases/download/v${ver}/gh_${ver}_linux_${arch}.deb" || {
    rm -f "$deb"
    return 1
  }
  if ! $run dpkg -i "$deb"; then
    $run apt-get update -qq
    $run apt-get install -y -f -qq
  fi
  rm -f "$deb"
  command -v gh >/dev/null 2>&1 && gh skill --help >/dev/null 2>&1
}

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

install_fspure_cli() {
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
  local dest="${HOME}/.local/bin/fspure"
  local url="https://github.com/e-St/fspure/releases/download/${FSPURE_CLI_RELEASE}/fspure"
  echo "==> Installing fspure CLI (${FSPURE_CLI_RELEASE}) → ${dest}"
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

install_copilot_skill() {
  ensure_github_cli || true
  if ! command -v gh >/dev/null 2>&1; then
    echo "WARNING: gh not on PATH; skip fspure Copilot skill." >&2
    return 0
  fi
  if ! gh skill --help >/dev/null 2>&1; then
    echo "WARNING: gh skill is unavailable (need GitHub CLI 2.90+); skip fspure Copilot skill." >&2
    return 0
  fi
  # Non-interactive: without --agent, gh prompts and Codespaces cancel.
  # --pin: latest GitHub Release tag (v0.4.0) does not contain the skill.
  export GH_PROMPT_DISABLED=1
  export GIT_TERMINAL_PROMPT=0
  if gh skill install e-St/fspure fspure-reduce-impurity \
    --scope user \
    --pin "$FSPURE_SKILL_REF" \
    --force \
    --agent github-copilot; then
    echo "✅ Copilot skill fspure-reduce-impurity (user scope, pin ${FSPURE_SKILL_REF})"
    return 0
  fi
  echo "WARNING: could not install e-St/fspure fspure-reduce-impurity (gh auth / network / ref ${FSPURE_SKILL_REF}?)." >&2
}

install_fspure_cli || true
install_copilot_skill

echo ""
echo "✅ fspure setup done."
echo "   Analyzer: $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll (pin $FSPURE_ANALYZER_VERSION)"
echo "   CLI: $(command -v fspure 2>/dev/null || echo 'not on PATH — rebuild the container')"
echo "   If pure/impure labels are missing: Developer: Reload Window"
