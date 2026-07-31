#!/usr/bin/env bash
# Install latest analyzer (nuget.org) + decorations extension (Open VSX).
set -euo pipefail
cd "$(dirname "$0")"
bash install-analyzer-nuget.sh
bash install-openvsx-extension.sh
