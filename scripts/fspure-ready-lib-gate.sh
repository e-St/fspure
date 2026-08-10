#!/usr/bin/env bash
# Phase 4 gate (KISS): prove fspure-ready-lib end-to-end from monorepo sources only.
#
#   1. Pack FSharp.PureAnalyzer → local feed
#   2. Pack Fspure.ReadyLib against that package (embed pure.json)
#   3. Assert embed on DLL + nupkg
#   4. Consumer restores ReadyLib from local feed
#   5. fsharp-analyzers + hard PURE002/PURE003 asserts
#
# No nuget.org analyzer, no GitHub Packages, no satellite.
#
# Usage (from fspure repo root):
#   bash scripts/fspure-ready-lib-gate.sh
#
# Env:
#   CONFIGURATION   default: Release
#   GATE_VERSION    default: 0.0.0-ci  (both packages)

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIGURATION="${CONFIGURATION:-Release}"
VERSION="${GATE_VERSION:-0.0.0-ci}"
SAMPLE="$ROOT/samples/fspure-ready-lib"
FEED="$ROOT/artifacts/local-feed"
ART="$ROOT/artifacts/fspure-ready-lib-gate"
ANALYZER_DROP="$ART/analyzer-drop/dotnet/fs"
REPORT="$ART/consumer.sarif"
STDOUT="$ART/analyzer-stdout.txt"

die() { echo "ERROR: $*" >&2; exit 1; }
ok() { echo "OK: $*"; }
step() { echo ""; echo "==> $*"; }

[[ -f "$ROOT/src/FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj" ]] \
  || die "run from fspure monorepo (FSharp.PureAnalyzer missing)"
[[ -f "$SAMPLE/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj" ]] \
  || die "sample missing: samples/fspure-ready-lib"

mkdir -p "$FEED" "$ANALYZER_DROP" "$ART"
rm -f "$FEED"/*.nupkg "$FEED"/*.snupkg 2>/dev/null || true

# Avoid stale global packages for this gate version.
GPF="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
rm -rf "$GPF/fsharp.pureanalyzer/${VERSION,,}" 2>/dev/null || true
rm -rf "$GPF/fspure.readylib/${VERSION,,}" 2>/dev/null || true
# NuGet folder names are lowercase package ids.
rm -rf "$GPF/fsharp.pureanalyzer/$VERSION" 2>/dev/null || true
rm -rf "$GPF/fspure.readylib/$VERSION" 2>/dev/null || true

# Local feed NuGet.Config: local first, then nuget.org (FSharp.Core etc.).
cat >"$FEED/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-feed" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

paket_restore() {
  local dir="$1"
  if [[ -f "$dir/paket.dependencies" ]]; then
    pushd "$dir" >/dev/null
    if command -v paket >/dev/null 2>&1; then
      paket restore
    elif [[ -x "$HOME/.dotnet/tools/paket" ]]; then
      "$HOME/.dotnet/tools/paket" restore
    else
      die "paket not found (needed for $dir)"
    fi
    popd >/dev/null
  fi
}

# ---------------------------------------------------------------------------
step "Pack FSharp.PureAnalyzer $VERSION → $FEED"
# ---------------------------------------------------------------------------
paket_restore "$ROOT/src/FSharp.PureAnalyzer"
paket_restore "$ROOT/src/fspure-collector"

dotnet pack "$ROOT/src/FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj" \
  -c "$CONFIGURATION" \
  -o "$FEED" \
  --nologo \
  -v minimal \
  "/p:Version=$VERSION" \
  "/p:PackageVersion=$VERSION"

ANALYZER_NUPKG="$(ls "$FEED"/FSharp.PureAnalyzer."$VERSION"*.nupkg 2>/dev/null | head -1 || true)"
[[ -n "$ANALYZER_NUPKG" && -f "$ANALYZER_NUPKG" ]] || die "analyzer nupkg not produced in $FEED"
ok "analyzer nupkg: $ANALYZER_NUPKG"

# Require Phase 3 package layout inside the nupkg.
TMP_AN="$(mktemp -d)"
trap 'rm -rf "$TMP_AN"' EXIT
unzip -q "$ANALYZER_NUPKG" -d "$TMP_AN"
[[ -f "$TMP_AN/build/FSharp.PureAnalyzer.targets" ]] \
  || die "packed analyzer missing build/FSharp.PureAnalyzer.targets"
if [[ ! -f "$TMP_AN/tools/fspure-collector/fspure-collector.dll" \
   && ! -f "$TMP_AN/tools/purity-collector/purity-collector.dll" ]]; then
  die "packed analyzer missing tools/fspure-collector/ (or legacy purity-collector)"
fi
[[ -f "$TMP_AN/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll" ]] \
  || die "packed analyzer missing analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
[[ -f "$TMP_AN/analyzers/dotnet/fs/FSharp.PureSchema.dll" ]] \
  || die "packed analyzer missing analyzers/dotnet/fs/FSharp.PureSchema.dll"
cp -f "$TMP_AN/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll" "$ANALYZER_DROP/"
cp -f "$TMP_AN/analyzers/dotnet/fs/FSharp.PureSchema.dll" "$ANALYZER_DROP/"
ok "Phase 3 package layout (build/ + tools/ + analyzers/)"

# ---------------------------------------------------------------------------
step "Pack Fspure.ReadyLib $VERSION (embed pure.json)"
# ---------------------------------------------------------------------------
dotnet pack "$SAMPLE/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj" \
  -c "$CONFIGURATION" \
  -o "$FEED" \
  --nologo \
  -v minimal \
  --configfile "$FEED/nuget.config" \
  "/p:Version=$VERSION" \
  "/p:PackageVersion=$VERSION" \
  "/p:FspureAnalyzerVersion=$VERSION" \
  "/p:RestoreForce=true"

LIB_NUPKG="$(ls "$FEED"/Fspure.ReadyLib."$VERSION"*.nupkg 2>/dev/null | head -1 || true)"
[[ -n "$LIB_NUPKG" && -f "$LIB_NUPKG" ]] || die "ReadyLib nupkg not produced"
ok "ReadyLib nupkg: $LIB_NUPKG"

DLL="$(find "$SAMPLE/src/Fspure.ReadyLib/bin" -name 'Fspure.ReadyLib.dll' 2>/dev/null | head -1 || true)"
[[ -n "$DLL" && -f "$DLL" ]] || die "Fspure.ReadyLib.dll not found after pack"

# ---------------------------------------------------------------------------
step "Assert embedded pure.json (DLL)"
# ---------------------------------------------------------------------------
dotnet run --project "$SAMPLE/tests/AssertEmbed/AssertEmbed.fsproj" -c "$CONFIGURATION" -- \
  "$DLL" \
  "Fspure.ReadyLib.Api.add" \
  "Fspure.ReadyLib.Api.mul" \
  "Fspure.ReadyLib.Api.manualEscapeHatch"
ok "DLL embed"

# ---------------------------------------------------------------------------
step "Assert embedded pure.json (nupkg)"
# ---------------------------------------------------------------------------
bash "$SAMPLE/scripts/assert-nupkg-embed.sh" "$LIB_NUPKG"
ok "nupkg embed"

# ---------------------------------------------------------------------------
step "Restore + build consumer from local feed"
# ---------------------------------------------------------------------------
dotnet restore "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  --configfile "$FEED/nuget.config" \
  "/p:FspureReadyLibVersion=$VERSION" \
  "/p:RestoreForce=true"

dotnet build "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  -c "$CONFIGURATION" \
  --nologo \
  --configfile "$FEED/nuget.config" \
  "/p:FspureReadyLibVersion=$VERSION" \
  --no-restore
ok "consumer built"

# ---------------------------------------------------------------------------
step "Run fsharp-analyzers on consumer"
# ---------------------------------------------------------------------------
if [[ -f "$SAMPLE/dotnet-tools.json" ]]; then
  (cd "$SAMPLE" && dotnet tool restore)
  run_analyzers() { (cd "$SAMPLE" && dotnet tool run fsharp-analyzers -- "$@"); }
elif [[ -f "$ROOT/dotnet-tools.json" ]] && grep -q 'fsharp-analyzers' "$ROOT/dotnet-tools.json"; then
  dotnet tool restore
  run_analyzers() { dotnet tool run fsharp-analyzers -- "$@"; }
else
  TOOL_DIR="$ART/tools"
  mkdir -p "$TOOL_DIR"
  if [[ ! -x "$TOOL_DIR/fsharp-analyzers" ]]; then
    dotnet tool install fsharp-analyzers --version 0.35.0 --tool-path "$TOOL_DIR"
  fi
  run_analyzers() { "$TOOL_DIR/fsharp-analyzers" "$@"; }
fi

set +e
run_analyzers \
  --project "$SAMPLE/tests/Consumer/Consumer.fsproj" \
  --analyzers-path "$ART/analyzer-drop" \
  --configuration "$CONFIGURATION" \
  --verbosity normal \
  --report "$REPORT" \
  2>&1 | tee "$STDOUT"
ANALYZER_EXIT=$?
set -e

BODY="$(cat "$STDOUT" 2>/dev/null || true)"
if [[ -f "$REPORT" ]]; then
  BODY="${BODY}$(cat "$REPORT")"
fi

# ---------------------------------------------------------------------------
step "Hard asserts (library embed must drive pure/impure labels)"
# ---------------------------------------------------------------------------
assert_contains() {
  local needle="$1"
  local label="$2"
  if ! grep -Fq "$needle" <<<"$BODY"; then
    echo "---- analyzer output (tail) ----" >&2
    tail -n 80 <<<"$BODY" >&2 || true
    die "$label — missing: $needle"
  fi
  ok "$label"
}

assert_absent() {
  local needle="$1"
  local label="$2"
  if grep -Fq "$needle" <<<"$BODY"; then
    die "$label — must not appear: $needle"
  fi
  ok "$label"
}

# Exact diagnostic messages from Diagnostics.fs
assert_contains "Function 'Consumer.useAdd' is transitively pure." \
  "useAdd PURE003 (library embed consumed)"
assert_contains "Function 'Consumer.useImpure' is not transitively pure." \
  "useImpure PURE002"
assert_contains "Function 'Consumer.useFoundational' is transitively pure." \
  "useFoundational PURE003 (foundational still works)"
assert_contains "Function 'Consumer.useMap' is transitively pure." \
  "useMap PURE003"

assert_absent "Function 'Consumer.useAdd' is not transitively pure." \
  "useAdd must not be impure"
assert_absent "Function 'Consumer.useImpure' is transitively pure." \
  "useImpure must not be pure"

# Codes present (sanity)
assert_contains "PURE002" "code PURE002 present"
assert_contains "PURE003" "code PURE003 present"

# Copy nupkgs into ART for workflow upload
cp -f "$ANALYZER_NUPKG" "$LIB_NUPKG" "$ART/" 2>/dev/null || true

echo ""
echo "✅ fspure-ready-lib gate green"
echo "   version:  $VERSION"
echo "   feed:     $FEED"
echo "   artifacts:$ART"
echo "   analyzer exit (informational): $ANALYZER_EXIT"
exit 0
