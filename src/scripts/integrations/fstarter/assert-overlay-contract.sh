#!/usr/bin/env bash
# Guard the fstarter overlay: a fspure → fstarter PR must not undo e-St/fstarter.
#
# Source this file, then: assert_fstarter_overlay_contract /path/with/.devcontainer
assert_fstarter_overlay_contract() {
  local root="$1"
  local dc_json="$root/.devcontainer/devcontainer.json"
  local setup="$root/.devcontainer/setup-fspure.sh"
  if [[ ! -f "$dc_json" ]]; then
    echo "ERROR: missing $dc_json" >&2
    return 1
  fi
  if [[ ! -f "$setup" ]]; then
    echo "ERROR: missing $setup" >&2
    return 1
  fi

  if ! python3 - "$dc_json" <<'PY'
import json, sys
from pathlib import Path

data = json.loads(Path(sys.argv[1]).read_text())
errors = []
if "postAttachCommand" in data:
    errors.append("postAttachCommand must not be present (postCreate-only)")
if "features" in data:
    errors.append("features must not be present (no github-cli feature)")
if data.get("postCreateCommand") != "bash .devcontainer/setup-fspure.sh":
    errors.append(
        "postCreateCommand must be bash .devcontainer/setup-fspure.sh, "
        f"got {data.get('postCreateCommand')!r}"
    )
settings = data.get("customizations", {}).get("vscode", {}).get("settings", {})
paths = settings.get("FSharp.analyzersPath") or []
if "/usr/local/share/fspure/analyzers" not in paths:
    errors.append("FSharp.analyzersPath must include /usr/local/share/fspure/analyzers")
exts = data.get("customizations", {}).get("vscode", {}).get("extensions") or []
if "e-st.fsharp-pure-decorations" not in exts:
    errors.append("must list e-st.fsharp-pure-decorations (Open VSX only; still unpack VSIX in setup)")
if errors:
    print("ERROR: overlay would undo e-St/fstarter:", file=sys.stderr)
    for err in errors:
        print(f"  - {err}", file=sys.stderr)
    sys.exit(1)
PY
  then
    echo "ERROR: devcontainer.json would undo e-St/fstarter ($root)" >&2
    return 1
  fi

  if ! python3 - "$setup" <<'PY'
import pathlib, sys

text = pathlib.Path(sys.argv[1]).read_text()
errors = []
if "skip extension install" in text:
    errors.append("must not skip decorations VSIX when the code CLI is unusable")
if "unpack_vsix" not in text:
    errors.append("must unpack the decorations VSIX into VS Code extension folders")
has_baked_vsix = (
    "/usr/local/share/fspure/fsharp-pure-decorations.vsix" in text
    or (
        'BAKED_ROOT="/usr/local/share/fspure"' in text
        and "fsharp-pure-decorations.vsix" in text
        and "BAKED_VSIX=" in text
    )
)
if not has_baked_vsix:
    errors.append("must install from /usr/local/share/fspure/fsharp-pure-decorations.vsix when present")
has_baked_analyzers = (
    "/usr/local/share/fspure/analyzers/dotnet/fs" in text
    or (
        'BAKED_ROOT="/usr/local/share/fspure"' in text
        and "analyzers/dotnet/fs" in text
        and "BAKED_ANALYZERS=" in text
    )
)
if not has_baked_analyzers:
    errors.append("must prefer baked analyzer at /usr/local/share/fspure/analyzers/dotnet/fs")
for needle in (".vscode-remote/extensions", ".vscode-server/extensions"):
    if needle not in text:
        errors.append(f"must unpack VSIX into {needle}")
if "extension.vsixmanifest" not in text or "dest/.vsixmanifest" not in text:
    errors.append("must copy extension.vsixmanifest to .vsixmanifest")
if "extensions.json" not in text:
    errors.append("must register the extension id in extensions.json")
if "install_extension || true" in text:
    errors.append("must fail setup if e-st.fsharp-pure-decorations is still missing")
if not any(line.strip() == "install_extension" for line in text.splitlines()):
    errors.append("must call install_extension (fail setup if the extension is missing)")
if "--agent github-copilot" not in text:
    errors.append("skill fallback must pass --agent github-copilot")
if "--scope user" not in text:
    errors.append("skill fallback must pass --scope user")
if "--pin" not in text:
    errors.append("skill fallback must pass --pin")
if errors:
    print("ERROR: setup-fspure.sh would undo e-St/fstarter:", file=sys.stderr)
    for err in errors:
        print(f"  - {err}", file=sys.stderr)
    sys.exit(1)
PY
  then
    echo "ERROR: setup-fspure.sh would undo e-St/fstarter ($root)" >&2
    return 1
  fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  set -euo pipefail
  assert_fstarter_overlay_contract "${1:-$(cd "$(dirname "$0")/overlay" && pwd)}"
fi
