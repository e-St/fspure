#!/usr/bin/env bash
# Install fspure into an fstarter Codespace / dev container.
# Pins win. Copy image artifacts only when /usr/local/share/fspure/versions.env
# matches .devcontainer/fspure-versions.env. A stale bake must not stay on PATH
# or in Ionide's analyzer drop.
#
# - FSharp.PureAnalyzer → workspace analyzers/dotnet/fs/ (Ionide; ahead of the bake)
# - fsharp-pure-decorations at FSPURE_EXTENSION_VERSION (Open VSX; baked VSIX if it matches)
# - standalone `fspure` CLI on PATH (~/.local/bin ahead of /usr/local/bin)
# - Copilot skill fspure-reduce-impurity at FSPURE_SKILL_REF
#
# Version pin: env, or .devcontainer/fspure-versions.env
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
BAKED_ROOT="/usr/local/share/fspure"
BAKED_ANALYZERS="$BAKED_ROOT/analyzers/dotnet/fs"
BAKED_SKILL="$BAKED_ROOT/skills/fspure-reduce-impurity/SKILL.md"
BAKED_VSIX="$BAKED_ROOT/fsharp-pure-decorations.vsix"
USER_SKILL="${HOME}/.copilot/skills/fspure-reduce-impurity/SKILL.md"

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
# Empty means Open VSX latest (older pins files).
FSPURE_EXTENSION_VERSION="${FSPURE_EXTENSION_VERSION:-}"

read_env_value() {
  local file="$1" key="$2"
  [[ -f "$file" ]] || return 1
  python3 - "$file" "$key" <<'PY'
import sys
path, key = sys.argv[1:]
for line in open(path, encoding="utf-8"):
    line = line.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    k, _, v = line.partition("=")
    if k.strip() == key:
        print(v.strip())
        raise SystemExit(0)
raise SystemExit(1)
PY
}

# Image bake records the versions it copied. A newer pin must not reuse that bake.
baked_analyzer_matches_pin() {
  local baked
  baked="$(read_env_value "$BAKED_ROOT/versions.env" FSPURE_ANALYZER_VERSION 2>/dev/null || true)"
  [[ -n "$baked" && "$baked" == "$FSPURE_ANALYZER_VERSION" ]]
}

vsix_version() {
  unzip -p "$1" extension/package.json \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["version"])'
}

cli_release_size() {
  curl -fsSL --retry 2 --retry-delay 1 \
    -H "Accept: application/vnd.github+json" \
    -H "User-Agent: fspure-setup" \
    "https://api.github.com/repos/e-St/fspure/releases/tags/${FSPURE_CLI_RELEASE}" \
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
  local bin="$1" remote size
  [[ -x "$bin" ]] || return 1
  "$bin" analyze --help >/dev/null 2>&1 || return 1
  remote="$(cli_release_size 2>/dev/null || true)"
  [[ -n "$remote" ]] || return 0
  size="$(stat -c %s "$bin" 2>/dev/null || echo 0)"
  [[ "$size" == "$remote" ]]
}

code_cli_usable() {
  command -v code >/dev/null 2>&1 || return 1
  local out
  if ! out="$(code --version 2>&1)"; then
    return 1
  fi
  [[ "$out" != *"not installed"* ]] || return 1
  return 0
}

copy_analyzer_dlls() {
  local src_dir="$1"
  local src schema
  src="$src_dir/FSharp.PureAnalyzer.dll"
  [[ -f "$src" ]] || return 1
  schema="$src_dir/FSharp.PureSchema.dll"
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

mirror_from_baked() {
  copy_analyzer_dlls "$BAKED_ANALYZERS"
}

mirror_from_global() {
  local src pkg_dir
  pkg_dir="$GLOBAL_PACKAGES/fsharp.pureanalyzer/${FSPURE_ANALYZER_VERSION}"
  src="$pkg_dir/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
  if [[ ! -f "$src" ]]; then
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
  copy_analyzer_dlls "$(dirname "$src")"
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

# postCreate often runs before the `code` CLI works. Do not skip this: the
# pure/impure labels come from this extension, not from Ionide LineLens.
extension_dirs() {
  local d
  for d in \
    "${HOME}/.vscode-remote/extensions" \
    "${HOME}/.vscode-server/extensions" \
    ${VSCODE_EXTENSIONS:+"$VSCODE_EXTENSIONS"}; do
    printf '%s\n' "$d"
  done
}

extension_on_disk() {
  local d
  while IFS= read -r d; do
    [[ -d "$d" ]] || continue
    if compgen -G "$d/${PUBLISHER_EXT}-*" > /dev/null; then
      return 0
    fi
  done < <(extension_dirs)
  return 1
}

extension_version_on_disk() {
  local want="$1" d
  [[ -n "$want" ]] || return 1
  while IFS= read -r d; do
    [[ -d "$d/${PUBLISHER_EXT}-${want}" ]] && return 0
  done < <(extension_dirs)
  return 1
}

register_extension_json() {
  local ext_root="$1"
  local dest="$2"
  local publisher="$3"
  local name="$4"
  local version="$5"
  local json="$ext_root/extensions.json"
  python3 - "$json" "$dest" "$publisher.$name" "$version" <<'PY'
import json, os, sys, time
path, dest, ext_id, version = sys.argv[1:5]
entries = []
if os.path.isfile(path):
    try:
        with open(path, encoding="utf-8") as f:
            entries = json.load(f)
        if not isinstance(entries, list):
            entries = []
    except json.JSONDecodeError:
        entries = []
entries = [e for e in entries if (e.get("identifier") or {}).get("id") != ext_id]
entries.append({
    "identifier": {"id": ext_id},
    "version": version,
    "location": {"$mid": 1, "path": dest, "scheme": "file"},
    "relativeLocation": os.path.basename(dest),
    "metadata": {
        "installedTimestamp": int(time.time() * 1000),
        "pinned": True,
        "source": "vsix",
    },
})
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as f:
    json.dump(entries, f)
PY
}

unpack_vsix() {
  local vsix="$1"
  local tmp pkg publisher name version dest d
  tmp="$(mktemp -d)"
  unzip -qo "$vsix" -d "$tmp"
  pkg="$tmp/extension/package.json"
  if [[ ! -f "$pkg" ]]; then
    rm -rf "$tmp"
    echo "ERROR: $vsix is not a VS Code VSIX (missing extension/package.json)" >&2
    return 1
  fi
  publisher="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["publisher"])' "$pkg")"
  name="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["name"])' "$pkg")"
  version="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$pkg")"
  while IFS= read -r d; do
    mkdir -p "$d"
    dest="$d/${publisher}.${name}-${version}"
    mkdir -p "$dest"
    cp -a "$tmp/extension/." "$dest/"
    if [[ -f "$tmp/extension.vsixmanifest" ]]; then
      cp -f "$tmp/extension.vsixmanifest" "$dest/.vsixmanifest"
    fi
    register_extension_json "$d" "$dest" "$publisher" "$name" "$version"
    echo "    unpacked → $dest"
  done < <(extension_dirs)
  rm -rf "$tmp"
}

download_openvsx_vsix() {
  local dest="$1"
  local url
  url="$(curl -fsSL "$OPENVSX_API" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
  curl -fsSL -o "$dest" "$url"
}

install_extension() {
  local vsix="" tmp="" baked_ver="" api url
  if [[ -n "${FSPURE_EXTENSION_VERSION:-}" ]] && extension_version_on_disk "$FSPURE_EXTENSION_VERSION"; then
    echo "✅ $PUBLISHER_EXT ${FSPURE_EXTENSION_VERSION} already on disk"
    return 0
  fi
  if [[ -z "${FSPURE_EXTENSION_VERSION:-}" ]] && extension_on_disk; then
    echo "✅ $PUBLISHER_EXT already on disk"
    return 0
  fi
  if [[ -f "$BAKED_VSIX" ]]; then
    baked_ver="$(vsix_version "$BAKED_VSIX" 2>/dev/null || true)"
  fi
  if [[ -n "$baked_ver" && ( -z "${FSPURE_EXTENSION_VERSION:-}" || "$baked_ver" == "$FSPURE_EXTENSION_VERSION" ) ]]; then
    vsix="$BAKED_VSIX"
    echo "==> fsharp-pure-decorations: baked VSIX ${baked_ver}"
  else
    tmp="$(mktemp --suffix=.vsix)"
    if [[ -n "${FSPURE_EXTENSION_VERSION:-}" ]]; then
      api="https://open-vsx.org/api/e-St/fsharp-pure-decorations/${FSPURE_EXTENSION_VERSION}"
      echo "==> fsharp-pure-decorations ${FSPURE_EXTENSION_VERSION}: Open VSX"
    else
      api="$OPENVSX_API"
      echo "==> fsharp-pure-decorations: Open VSX latest"
    fi
    if url="$(curl -fsSL "$api" | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")" \
      && curl -fsSL -o "$tmp" "$url"; then
      vsix="$tmp"
    else
      rm -f "$tmp"
      tmp=""
      if [[ -n "${FSPURE_EXTENSION_VERSION:-}" && -n "$baked_ver" && "$baked_ver" != "$FSPURE_EXTENSION_VERSION" ]]; then
        echo "ERROR: Open VSX ${FSPURE_EXTENSION_VERSION} unavailable; refusing baked VSIX ${baked_ver}" >&2
      elif [[ -f "$BAKED_VSIX" ]]; then
        echo "WARNING: could not download extension; using baked VSIX ${baked_ver:-unknown}" >&2
        vsix="$BAKED_VSIX"
      fi
    fi
  fi
  if [[ -z "$vsix" ]]; then
    echo "ERROR: no fsharp-pure-decorations VSIX (baked missing, Open VSX failed)." >&2
    return 1
  fi
  unpack_vsix "$vsix"
  if code_cli_usable; then
    code --install-extension "$vsix" --force >/dev/null || true
  else
    echo "    code CLI not usable; installed via filesystem unpack"
  fi
  [[ -z "$tmp" ]] || rm -f "$tmp"
  if [[ -n "${FSPURE_EXTENSION_VERSION:-}" ]] && extension_version_on_disk "$FSPURE_EXTENSION_VERSION"; then
    echo "✅ $PUBLISHER_EXT ${FSPURE_EXTENSION_VERSION} on disk (pure/impure labels)"
    return 0
  fi
  if [[ -z "${FSPURE_EXTENSION_VERSION:-}" ]] && extension_on_disk; then
    echo "✅ $PUBLISHER_EXT on disk (pure/impure labels)"
    return 0
  fi
  echo "ERROR: could not install $PUBLISHER_EXT ${FSPURE_EXTENSION_VERSION:-}; pure/impure labels will not show." >&2
  return 1
}

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
  local dest="${HOME}/.local/bin/fspure"
  # ~/.local/bin is ahead of the image binary. Replace it when the release changed.
  if cli_matches_release "$dest"; then
    echo "✅ fspure CLI ${dest} (${FSPURE_CLI_RELEASE})"
    return 0
  fi
  if cli_matches_release /usr/local/bin/fspure && [[ ! -e "$dest" ]]; then
    echo "✅ fspure CLI /usr/local/bin/fspure (${FSPURE_CLI_RELEASE})"
    return 0
  fi
  case "$(uname -m)" in
    x86_64 | amd64) ;;
    *)
      echo "WARNING: no standalone fspure binary for $(uname -m); skip CLI install." >&2
      return 1
      ;;
  esac
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

skill_matches_pin() {
  [[ -f "$USER_SKILL" ]] || return 1
  python3 - "$USER_SKILL" "$FSPURE_SKILL_REF" <<'PY'
import sys
text = open(sys.argv[1], encoding="utf-8").read(8000)
pin = sys.argv[2]
ok = f"github-pinned: {pin}" in text or f"github-ref: refs/tags/{pin}" in text
raise SystemExit(0 if ok else 1)
PY
}

install_copilot_skill() {
  mkdir -p "$(dirname "$USER_SKILL")"
  if skill_matches_pin; then
    echo "✅ Copilot skill fspure-reduce-impurity (${FSPURE_SKILL_REF})"
    return 0
  fi
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
  local baked_ref=""
  baked_ref="$(read_env_value "$BAKED_ROOT/versions.env" FSPURE_SKILL_REF 2>/dev/null || true)"
  if [[ -f "$BAKED_SKILL" && ( -z "$baked_ref" || "$baked_ref" == "$FSPURE_SKILL_REF" ) ]]; then
    cp -f "$BAKED_SKILL" "$USER_SKILL"
    echo "✅ Copilot skill fspure-reduce-impurity (baked, matches pin)"
  fi
}

echo "==> fspure setup (fstarter)"
echo "    analyzer pin: $FSPURE_ANALYZER_VERSION"

if baked_analyzer_matches_pin && mirror_from_baked; then
  echo "✅ FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION} mirrored from image (matches pin)"
elif install_analyzer_nuget && mirror_from_global; then
  echo "✅ FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION} installed from nuget.org"
else
  echo "ERROR: could not install FSharp.PureAnalyzer ${FSPURE_ANALYZER_VERSION} (baked image does not match the pin)" >&2
  exit 1
fi

install_extension
install_fspure_cli || true
install_copilot_skill

echo ""
echo "✅ fspure setup done."
echo "   Analyzer: $WORKSPACE_ANALYZERS/FSharp.PureAnalyzer.dll (pin $FSPURE_ANALYZER_VERSION)"
echo "   CLI: $(command -v fspure 2>/dev/null || echo 'not on PATH — rebuild the container')"
echo "   If pure/impure labels are missing: Developer: Reload Window"
