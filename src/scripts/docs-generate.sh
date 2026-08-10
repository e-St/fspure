#!/usr/bin/env bash
# DEPRECATED thin shim — prefer F# / Nix / Nushell entrypoints (no logic here):
#
#   nix run .#docs -- preview
#   nix run .#docs -- stable 0.4.0
#   fspure-docs preview                 # on PATH inside `nix develop` / direnv
#   dotnet run --project src/DocsGenerator -- preview
#   nu src/scripts/fspure.nu docs preview
#
# Kept only so existing CI YAML keeps working until workflows are fully flipped.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec dotnet run --project src/DocsGenerator/DocsGenerator.fsproj -c "${CONFIGURATION:-Release}" -- "$@"
