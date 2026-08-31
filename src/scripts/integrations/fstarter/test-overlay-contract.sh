#!/usr/bin/env bash
# Contract tests: overlay must match e-St/fstarter; prepare must refuse regressions.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../../../.." && pwd)"
# shellcheck source=assert-overlay-contract.sh
source "$HERE/assert-overlay-contract.sh"

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "ok: $*"; }

assert_fstarter_overlay_contract "$HERE/overlay" || fail "current overlay failed contract"
pass "current overlay"

scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$scratch/.devcontainer"
cp -f "$HERE/overlay/.devcontainer/devcontainer.json" "$scratch/.devcontainer/"
cp -f "$HERE/overlay/.devcontainer/setup-fspure.sh" "$scratch/.devcontainer/"

python3 - "$scratch/.devcontainer/devcontainer.json" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
data = json.loads(p.read_text())
data["postAttachCommand"] = "bash .devcontainer/setup-fspure.sh"
data["features"] = {"ghcr.io/devcontainers/features/github-cli:1": {}}
p.write_text(json.dumps(data, indent=2) + "\n")
PY
if assert_fstarter_overlay_contract "$scratch" >/dev/null 2>&1; then
  fail "accepted postAttachCommand + features"
fi
pass "rejects postAttachCommand + features"

cp -f "$HERE/overlay/.devcontainer/devcontainer.json" "$scratch/.devcontainer/"
python3 - "$scratch/.devcontainer/setup-fspure.sh" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1])
text = p.read_text()
text = text.replace(
    'echo "    code CLI not usable; installed via filesystem unpack"',
    'echo "WARNING: VS Code \'code\' CLI not usable; skip extension install." >&2\n    return 0',
)
p.write_text(text)
PY
if assert_fstarter_overlay_contract "$scratch" >/dev/null 2>&1; then
  fail "accepted setup that skips VSIX when code is unusable"
fi
pass "rejects skip-extension-install setup"

# Filesystem unpack is the source of truth (postCreate often has no usable `code`).
unpack_home="$(mktemp -d)"
vsix_src="$(mktemp -d)"
mkdir -p "$vsix_src/extension"
printf '%s\n' '{"publisher":"e-st","name":"fsharp-pure-decorations","version":"9.9.9"}' \
  >"$vsix_src/extension/package.json"
printf '%s\n' 'manifest' >"$vsix_src/extension.vsixmanifest"
python3 - "$vsix_src" "$unpack_home/ext.vsix" <<'PY'
import sys, zipfile
from pathlib import Path
src, dest = Path(sys.argv[1]), Path(sys.argv[2])
with zipfile.ZipFile(dest, "w") as z:
    z.write(src / "extension" / "package.json", "extension/package.json")
    z.write(src / "extension.vsixmanifest", "extension.vsixmanifest")
PY
fns="$(mktemp)"
awk '/^code_cli_usable\(\)/,/^ensure_github_cli\(\)/ {
  if (/^ensure_github_cli\(\)/) exit
  print
}' "$HERE/overlay/.devcontainer/setup-fspure.sh" >"$fns"
HOME="$unpack_home" PUBLISHER_EXT="e-st.fsharp-pure-decorations" bash -c '
  set -euo pipefail
  # shellcheck disable=SC1090
  source "$1"
  unpack_vsix "$2"
' bash "$fns" "$unpack_home/ext.vsix"
ext_dir="$unpack_home/.vscode-remote/extensions/e-st.fsharp-pure-decorations-9.9.9"
[[ -f "$ext_dir/package.json" ]] || fail "unpack missed ~/.vscode-remote/extensions"
[[ -f "$ext_dir/.vsixmanifest" ]] || fail "unpack missed .vsixmanifest"
[[ -f "$unpack_home/.vscode-server/extensions/e-st.fsharp-pure-decorations-9.9.9/package.json" ]] \
  || fail "unpack missed ~/.vscode-server/extensions"
python3 - "$unpack_home/.vscode-remote/extensions/extensions.json" <<'PY' || fail "unpack missed extensions.json id"
import json, sys
from pathlib import Path
entries = json.loads(Path(sys.argv[1]).read_text())
ids = {(e.get("identifier") or {}).get("id") for e in entries}
assert "e-st.fsharp-pure-decorations" in ids, ids
PY
pass "unpacks VSIX without code CLI"
rm -rf "$vsix_src" "$fns"

dest="$(mktemp -d)"
trap 'rm -rf "$scratch" "$dest"' EXIT
mkdir -p "$dest/.devcontainer"
bash "$ROOT/src/scripts/prepare-fstarter-update.sh" "$dest" 0.4.0 >/dev/null
[[ -f "$dest/.devcontainer/setup-fspure.sh" ]] || fail "prepare did not copy setup"
if grep -q 'postAttachCommand' "$dest/.devcontainer/devcontainer.json"; then
  fail "prepare copied postAttachCommand"
fi
if grep -q '"features"' "$dest/.devcontainer/devcontainer.json"; then
  fail "prepare copied features"
fi
grep -q '/usr/local/share/fspure/analyzers' "$dest/.devcontainer/devcontainer.json" || fail "prepare dropped baked analyzersPath"
grep -q 'unpack_vsix' "$dest/.devcontainer/setup-fspure.sh" || fail "prepare dropped VSIX unpack"
pass "prepare-fstarter-update.sh copies a contract-ok overlay"

echo "all overlay contract tests passed"
