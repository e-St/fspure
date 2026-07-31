#!/usr/bin/env bash
# Install latest fsharp-pure-decorations from Open VSX (VSIX — VS Code can't browse Open VSX).
set -euo pipefail
VSIX="$(mktemp --suffix=.vsix)"
trap 'rm -f "$VSIX"' EXIT
curl -fsSL -o "$VSIX" "$(curl -fsSL https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")"
code --install-extension "$VSIX" --force
