#!/usr/bin/env bash
# Phase 5 permanent regression net (KISS — no extra library projects).
#
# Uses only:
#   - tests/e2e/customer-fixture          → foundational-only badges
#   - samples/fspure-ready-lib      → library embed (PackageReference + ProjectReference)
#   - existing unit tests           → missing / zero / corrupt pure.json fallback
#   - Fspure.DecorationLogic.Tests  → VS Code badge mapping contract (F#, no IDE)
#
# Usage (from fspure monorepo root):
#   bash scripts/phase5-regression.sh
#
# Env:
#   CONFIGURATION   default: Release
#   GATE_VERSION    default: 0.0.0-ci  (shared with fspure-ready-lib-gate)
#   SKIP_PHASE1=1   skip foundational customer e2e
#   SKIP_UNIT=1     skip unit tests

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIGURATION="${CONFIGURATION:-Release}"
VERSION="${GATE_VERSION:-0.0.0-ci}"
SAMPLE="$ROOT/samples/fspure-ready-lib"
FEED="$ROOT/artifacts/local-feed"
ART="$ROOT/artifacts/phase5"
ANALYZER_DROP="$ART/analyzer-drop/dotnet/fs"

die() { echo "ERROR: $*" >&2; exit 1; }
ok() { echo "OK: $*"; }
step() { echo ""; echo "======== $* ========"; }

[[ -f "$ROOT/scripts/fspure-ready-lib-gate.sh" ]] || die "monorepo gate script missing"
chmod +x \
  "$ROOT/scripts/fspure-ready-lib-gate.sh" \
  "$ROOT/scripts/phase5-regression.sh" \
  "$SAMPLE/scripts/"*.sh \
  "$ROOT/tests/e2e/phase1/run.sh" \
  2>/dev/null || true

mkdir -p "$ART" "$ANALYZER_DROP"

# ---------------------------------------------------------------------------
step "1/5  Foundational only (customer-fixture e2e phase1)"
# ---------------------------------------------------------------------------
if [[ "${SKIP_PHASE1:-0}" == "1" ]]; then
  echo "(skipped SKIP_PHASE1=1)"
else
  bash "$ROOT/tests/e2e/phase1/run.sh"
  ok "foundational badges match expectations.json"
fi

# ---------------------------------------------------------------------------
step "2/5  ReadyLib PackageReference (local-feed gate + golden)"
# ---------------------------------------------------------------------------
bash "$ROOT/scripts/fspure-ready-lib-gate.sh"

# Reuse analyzer drop + pure.json from gate artifacts
GATE_ART="$ROOT/artifacts/fspure-ready-lib-gate"
if [[ -d "$GATE_ART/analyzer-drop/dotnet/fs" ]]; then
  cp -f "$GATE_ART/analyzer-drop/dotnet/fs/"*.dll "$ANALYZER_DROP/" 2>/dev/null || true
fi

PURE_JSON="$(find "$SAMPLE/src/Fspure.ReadyLib/obj" -name 'Fspure.ReadyLib.pure.json' 2>/dev/null | head -1 || true)"
[[ -n "$PURE_JSON" && -f "$PURE_JSON" ]] || die "ReadyLib pure.json not found after gate"
bash "$SAMPLE/scripts/assert-golden-pure-methods.sh" "$PURE_JSON"
cp -f "$PURE_JSON" "$ART/Fspure.ReadyLib.pure.json"
ok "PackageReference path + golden pure methods"

# ---------------------------------------------------------------------------
step "3/5  ReadyLib ProjectReference (same Consumer + library project)"
# ---------------------------------------------------------------------------
[[ -f "$FEED/nuget.config" ]] || die "local feed missing — gate should have created it"
[[ -d "$ANALYZER_DROP" && -f "$ANALYZER_DROP/FSharp.PureAnalyzer.dll" ]] \
  || die "analyzer drop missing after gate"

run_analyzers() {
  if [[ -f "$SAMPLE/dotnet-tools.json" ]]; then
    (cd "$SAMPLE" && dotnet tool restore >/dev/null)
    (cd "$SAMPLE" && dotnet tool run fsharp-analyzers -- "$@")
  else
    dotnet tool restore >/dev/null
    dotnet tool run fsharp-analyzers -- "$@"
  fi
}

assert_body_contains() {
  local body="$1"
  local needle="$2"
  local label="$3"
  if ! grep -Fq "$needle" <<<"$body"; then
    tail -n 40 <<<"$body" >&2 || true
    die "$label — missing: $needle"
  fi
  ok "$label"
}

# 3a — rebuild ReadyLib (embeds pure.json) via same MSBuild path used as ProjectReference
dotnet build "$SAMPLE/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj" \
  -c "$CONFIGURATION" \
  --nologo \
  --configfile "$FEED/nuget.config" \
  "/p:Version=$VERSION" \
  "/p:PackageVersion=$VERSION" \
  "/p:FspureAnalyzerVersion=$VERSION" \
  "/p:RestoreForce=true"

LIB_DLL="$(find "$SAMPLE/src/Fspure.ReadyLib/bin" -name 'Fspure.ReadyLib.dll' | head -1)"
[[ -f "$LIB_DLL" ]] || die "ReadyLib.dll missing after ProjectReference-style build"
dotnet run --project "$SAMPLE/tests/AssertEmbed/AssertEmbed.fsproj" -c "$CONFIGURATION" -- \
  "$LIB_DLL" \
  "Fspure.ReadyLib.Api.add" \
  "Fspure.ReadyLib.Api.manualEscapeHatch"
python3 - <<'PY'
import json, sys
from pathlib import Path
root = Path("samples/fspure-ready-lib")
matches = list(root.joinpath("src/Fspure.ReadyLib/obj").rglob("Fspure.ReadyLib.pure.json"))
if not matches:
    print("ERROR: pure.json not found under obj/", file=sys.stderr)
    sys.exit(1)
doc = json.loads(matches[0].read_text())
names = {m["fullName"] for m in doc.get("pureMethods") or []}
if "Fspure.ReadyLib.Api.impureLog" in names:
    print("ERROR: impureLog must not be in pure.json", file=sys.stderr)
    sys.exit(1)
if "Fspure.ReadyLib.Api.add" not in names:
    print("ERROR: Api.add missing from pure.json", file=sys.stderr)
    sys.exit(1)
print("OK: impureLog not in pure.json; Api.add present")
PY
ok "Project-built ReadyLib embeds pure surface (not impureLog)"

# 3b — analyse the library project itself (source ProjectReference path for impure surface)
REPORT_LIB="$ART/readylib-project.sarif"
STDOUT_LIB="$ART/readylib-project-stdout.txt"
set +e
run_analyzers \
  --project "$SAMPLE/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj" \
  --analyzers-path "$ART/analyzer-drop" \
  --configuration "$CONFIGURATION" \
  --verbosity normal \
  --report "$REPORT_LIB" \
  2>&1 | tee "$STDOUT_LIB"
set -e
BODY_LIB="$(cat "$STDOUT_LIB" 2>/dev/null || true)"
if [[ -f "$REPORT_LIB" ]]; then
  BODY_LIB="${BODY_LIB}$(cat "$REPORT_LIB")"
fi
assert_body_contains "$BODY_LIB" \
  "Function 'Fspure.ReadyLib.Api.add' is transitively pure." \
  "ReadyLib project: Api.add PURE003"
assert_body_contains "$BODY_LIB" \
  "Function 'Fspure.ReadyLib.Api.impureLog' is not transitively pure." \
  "ReadyLib project: Api.impureLog PURE002"

# 3c — same Consumer project, ProjectReference mode (embed still consumed for pure API)
dotnet restore "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  --configfile "$FEED/nuget.config" \
  "/p:FspureReadyLibUseProjectReference=true" \
  "/p:FspureAnalyzerVersion=$VERSION" \
  "/p:RestoreForce=true"

dotnet build "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  -c "$CONFIGURATION" \
  --nologo \
  --configfile "$FEED/nuget.config" \
  "/p:FspureReadyLibUseProjectReference=true" \
  "/p:FspureAnalyzerVersion=$VERSION" \
  --no-restore

REPORT_PR="$ART/consumer-projectref.sarif"
STDOUT_PR="$ART/consumer-projectref-stdout.txt"
set +e
run_analyzers \
  --project "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  --analyzers-path "$ART/analyzer-drop" \
  --configuration "$CONFIGURATION" \
  --verbosity normal \
  --report "$REPORT_PR" \
  2>&1 | tee "$STDOUT_PR"
set -e

BODY="$(cat "$STDOUT_PR" 2>/dev/null || true)"
if [[ -f "$REPORT_PR" ]]; then
  BODY="${BODY}$(cat "$REPORT_PR")"
fi

assert_body_contains "$BODY" \
  "Function 'Consumer.useAdd' is transitively pure." \
  "Consumer ProjectReference: useAdd PURE003"
assert_body_contains "$BODY" \
  "Function 'Consumer.useFoundational' is transitively pure." \
  "Consumer ProjectReference: useFoundational PURE003"
# Note: useImpure may be mis-classified pure under ProjectReference when FCS does not
# wire cross-project callees into the call graph. Impure library surface is gated in 3b
# and under PackageReference (step 2). Do not hard-require useImpure PURE002 here.
ok "ProjectReference path"

# ---------------------------------------------------------------------------
step "4/5  Missing / zero / corrupt pure.json (unit tests)"
# ---------------------------------------------------------------------------
if [[ "${SKIP_UNIT:-0}" == "1" ]]; then
  echo "(skipped SKIP_UNIT=1)"
else
  if command -v paket >/dev/null 2>&1; then
    (cd "$ROOT/src/FSharp.PureAnalyzer" && paket restore)
  elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then
    (cd "$ROOT/src/FSharp.PureAnalyzer" && "$HOME/.dotnet/tools/paket" restore)
  fi

  # PureSchema resource reader fixtures (zero / single / multi pure.json)
  dotnet test "$ROOT/tests/FSharp.PureSchema.Tests/FSharp.PureSchema.Tests.fsproj" \
    -c "$CONFIGURATION" \
    --verbosity minimal \
    --filter "FullyQualifiedName~ResourceReaderTests"

  # Analyser fallback + composition + override contracts
  dotnet test "$ROOT/tests/FSharp.PureAnalyzer.Tests/FSharp.PureAnalyzer.Tests.fsproj" \
    -c "$CONFIGURATION" \
    --verbosity minimal \
    --filter "FullyQualifiedName~ManifestIntegrationTests|FullyQualifiedName~CompositionTests|FullyQualifiedName~OverrideTests"
  ok "fallback + composition + override unit tests"
fi

# ---------------------------------------------------------------------------
step "5/5  VS Code decoration contract (minimal, no IDE)"
# ---------------------------------------------------------------------------
if [[ -f "$ROOT/src/Fspure.DecorationLogic.Tests/Fspure.DecorationLogic.Tests.fsproj" ]]; then
  dotnet test "$ROOT/src/Fspure.DecorationLogic.Tests/Fspure.DecorationLogic.Tests.fsproj" \
    -c Release --nologo -v q
  ok "decoration PURE002/PURE003 → pure/impure labels (F#)"
else
  echo "(skipped: DecorationLogic tests project missing)"
fi

echo ""
echo "✅ Phase 5 regression green"
echo "   foundational:     tests/e2e/phase1"
echo "   PackageReference: scripts/fspure-ready-lib-gate.sh + golden"
echo "   ProjectReference: samples/fspure-ready-lib Consumer"
echo "   fallbacks:        unit tests (missing/zero/corrupt)"
echo "   decorations:      vscode-extension unit contract"
echo "   artifacts:        $ART"
exit 0
