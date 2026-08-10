#!/usr/bin/env bash
# Local / CI vulnerability scan for fspure ecosystems.
#
# Exit non-zero if known vulnerable packages are reported.
# Usage (repo root):
#   bash scripts/security-audit.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIGURATION="${CONFIGURATION:-Release}"
FAIL=0

log() { echo "$*"; }
section() { echo ""; echo "======== $* ========"; }
die_soft() { echo "ERROR: $*" >&2; FAIL=1; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die_soft "missing command: $1"
}

require_cmd dotnet

# --- NuGet vulnerable package scan ---
section "NuGet: restore + list --vulnerable"

scan_nuget_project() {
  local proj="$1"
  if [[ ! -f "$proj" ]]; then
    log "skip missing $proj"
    return 0
  fi
  log "--> $proj"
  # Restore so assets exist for list package
  if ! dotnet restore "$proj" -v q >/dev/null 2>&1; then
    die_soft "restore failed: $proj"
    return 0
  fi
  local out
  out="$(dotnet list "$proj" package --vulnerable --include-transitive 2>&1 || true)"
  echo "$out"
  # Do not match the success phrase "has no vulnerable packages".
  if echo "$out" | grep -qiE 'has the following vulnerable packages'; then
    die_soft "vulnerable package(s) in $proj"
  fi
  if echo "$out" | grep -qiE 'GHSA-[0-9a-z-]+|CVE-[0-9]{4}-'; then
    die_soft "advisory id reported for $proj"
  fi
}

# Prefer paket restore for paket projects when available
if command -v paket >/dev/null 2>&1 || [[ -x "$HOME/.dotnet/tools/paket" ]]; then
  PAKET_BIN="$(command -v paket || echo "$HOME/.dotnet/tools/paket")"
  if [[ -f FSharp.PureAnalyzer/paket.dependencies ]]; then
    (cd FSharp.PureAnalyzer && "$PAKET_BIN" restore) || die_soft "paket restore FSharp.PureAnalyzer"
  fi
  if [[ -f fspure-collector/paket.dependencies ]]; then
    (cd fspure-collector && "$PAKET_BIN" restore) || die_soft "paket restore fspure-collector"
  fi
fi

scan_nuget_project "schema/FSharp.PureSchema/FSharp.PureSchema.fsproj"
scan_nuget_project "schema/FSharp.PureSchema.Tests/FSharp.PureSchema.Tests.fsproj"
scan_nuget_project "FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj"
scan_nuget_project "FSharp.PureAnalyzer.Tests/FSharp.PureAnalyzer.Tests.fsproj"
scan_nuget_project "fspure-collector/fspure-collector.fsproj"
scan_nuget_project "fspure-collector.Tests/fspure-collector.Tests.fsproj"
scan_nuget_project "msbuild/Fspure.BuildTasks/Fspure.BuildTasks.csproj"
# (C# by necessity for MSBuild task hosting — see docs/LANGUAGES.md)
scan_nuget_project "samples/fspure-ready-lib/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj"

# --- npm audit ---
section "npm audit (vscode-extension)"
if [[ -f vscode-extension/package.json ]]; then
  if command -v npm >/dev/null 2>&1; then
    (
      cd vscode-extension
      if [[ -f package-lock.json ]]; then
        npm ci --ignore-scripts 2>/dev/null || npm install --ignore-scripts
      else
        npm install --ignore-scripts --package-lock-only 2>/dev/null || npm install --ignore-scripts
      fi
      # High+ only for CI noise control; local can set NPM_AUDIT_LEVEL=moderate
      LEVEL="${NPM_AUDIT_LEVEL:-high}"
      if ! npm audit --audit-level="$LEVEL"; then
        die_soft "npm audit failed (level=$LEVEL) in vscode-extension"
      fi
    )
  else
    log "npm not installed — skip vscode-extension audit"
  fi
fi

section "npm audit (e2e/phase2/playwright)"
if [[ -f e2e/phase2/playwright/package.json ]]; then
  if command -v npm >/dev/null 2>&1; then
    (
      cd e2e/phase2/playwright
      if [[ -f package-lock.json ]]; then
        npm ci --ignore-scripts 2>/dev/null || npm install --ignore-scripts
      else
        npm install --ignore-scripts 2>/dev/null || true
      fi
      LEVEL="${NPM_AUDIT_LEVEL:-high}"
      if ! npm audit --audit-level="$LEVEL"; then
        die_soft "npm audit failed (level=$LEVEL) in e2e/phase2/playwright"
      fi
    ) || die_soft "playwright npm audit setup failed"
  else
    log "npm not installed — skip playwright audit"
  fi
fi

section "Summary"
if [[ "$FAIL" -ne 0 ]]; then
  echo "Security audit FAILED"
  exit 1
fi
echo "Security audit OK (NuGet vulnerable scan + npm audit)"
exit 0
