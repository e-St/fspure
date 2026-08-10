#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
exec dotnet run --project src/Fspure.Tasks/Fspure.Tasks.fsproj -c "${CONFIGURATION:-Release}" -- docs "$@"
