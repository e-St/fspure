#!/usr/bin/env bash
# Thin shim → F# (src/Fspure.Tasks / DocsGenerator). Do not add logic here.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
exec dotnet run --project src/Fspure.Tasks/Fspure.Tasks.fsproj -c "${CONFIGURATION:-Release}" -- docs "$@"
