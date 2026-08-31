#!/usr/bin/env bash
# Install fsharp-pure-decorations.
# Prefer packaging the in-repo VSIX (matches e2e phase2 / known-good labels).
# Fall back to the baked image VSIX, then Open VSX, if local packaging fails.
#
# postCreate often runs before the `code` CLI works. Do not skip this: the
# pure/impure labels come from this extension, not from Ionide LineLens.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
EXT_DIR="$ROOT/src/editor/vscode-extension"
PUBLISHER_EXT="e-st.fsharp-pure-decorations"
OPENVSX_API="https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest"
BAKED_VSIX="/usr/local/share/fspure/fsharp-pure-decorations.vsix"

# True only if `code` is on PATH and can report a version (stubs that print
# "code or code-insiders is not installed" count as unavailable).
code_cli_usable() {
  command -v code >/dev/null 2>&1 || return 1
  local out
  if ! out="$(code --version 2>&1)"; then
    return 1
  fi
  [[ "$out" != *"not installed"* ]] || return 1
  return 0
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

is_valid_vsix() {
  local vsix="$1"
  [[ -f "$vsix" && -s "$vsix" ]] || return 1
  unzip -tqq "$vsix" >/dev/null 2>&1 || return 1
  unzip -p "$vsix" extension/package.json >/dev/null 2>&1
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

package_local_vsix() {
  local dest="$1"
  if [[ ! -f "$EXT_DIR/package.json" ]]; then
    echo "    skip local package: missing $EXT_DIR/package.json" >&2
    return 1
  fi
  echo "==> fsharp-pure-decorations: package from local tree"
  if ! (
    cd "$EXT_DIR"
    npx --yes @vscode/vsce package --no-dependencies --allow-missing-repository --out "$dest"
  ); then
    echo "    local vsce package failed." >&2
    return 1
  fi
  if ! is_valid_vsix "$dest"; then
    echo "    local VSIX is not a valid zip with extension/package.json" >&2
    return 1
  fi
  return 0
}

download_openvsx_vsix() {
  local dest="$1"
  local url
  url="$(curl -fsSL "$OPENVSX_API" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
  curl -fsSL -o "$dest" "$url"
}

install_extension() {
  local vsix="" tmp=""
  if extension_on_disk; then
    echo "✅ $PUBLISHER_EXT already on disk"
    return 0
  fi

  tmp="$(mktemp --suffix=.vsix)"
  if package_local_vsix "$tmp"; then
    vsix="$tmp"
  elif [[ -f "$BAKED_VSIX" ]] && is_valid_vsix "$BAKED_VSIX"; then
    echo "==> fsharp-pure-decorations: baked VSIX"
    vsix="$BAKED_VSIX"
    rm -f "$tmp"
    tmp=""
  else
    echo "==> fsharp-pure-decorations: try Open VSX"
    if download_openvsx_vsix "$tmp" && is_valid_vsix "$tmp"; then
      vsix="$tmp"
    else
      echo "    Open VSX VSIX missing or invalid." >&2
      rm -f "$tmp"
      tmp=""
    fi
  fi

  if [[ -z "$vsix" ]]; then
    echo "ERROR: no fsharp-pure-decorations VSIX (local package failed, baked missing, Open VSX failed)." >&2
    return 1
  fi

  unpack_vsix "$vsix"
  if code_cli_usable; then
    code --install-extension "$vsix" --force >/dev/null || true
  else
    echo "    code CLI not usable; installed via filesystem unpack"
  fi
  [[ -z "$tmp" ]] || rm -f "$tmp"

  if extension_on_disk; then
    echo "✅ $PUBLISHER_EXT on disk (pure/impure labels)"
    return 0
  fi
  echo "ERROR: could not install $PUBLISHER_EXT; pure/impure labels will not show." >&2
  return 1
}

install_extension
