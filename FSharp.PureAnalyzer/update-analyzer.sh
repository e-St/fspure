#!/usr/bin/env bash
# Pack the local FSharp.PureAnalyzer and install it into the NuGet global packages
# folder so Ionide (FSharp.analyzersPath: ${userHome}/.nuget/packages/fsharp.pureanalyzer)
# and `dotnet add package` consumers pick up the in-repo build.
#
# Usage:
#   bash FSharp.PureAnalyzer/update-analyzer.sh
#   VERSION=0.1.0-local CONFIGURATION=Release bash FSharp.PureAnalyzer/update-analyzer.sh
#
# After install: Developer: Reload Window (or restart FSAC) so Ionide reloads analyzers.

set -euo pipefail

# NuGet scratch dir: avoid /tmp chmod 700 failures under Docker/CI mounts.
export TMPDIR="${TMPDIR:-${HOME}/.cache/nuget-tmp}"
export TEMP="${TEMP:-$TMPDIR}"
export TMP="${TMP:-$TMPDIR}"
mkdir -p "$TMPDIR"

cd "$(dirname "$0")"
PROJ_DIR="$(pwd)"
ROOT="$(cd .. && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
PACKAGE_ID="FSharp.PureAnalyzer"
PACKAGE_ID_LOWER="fsharp.pureanalyzer"
NUPKG_OUT="${NUPKG_OUT:-$PROJ_DIR/nupkgs}"
GLOBAL_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

# Version: VERSION env wins, else <Version> from the fsproj.
if [[ -z "${VERSION:-}" ]]; then
  VERSION="$(python3 - <<'PY'
import re, pathlib
text = pathlib.Path("FSharp.PureAnalyzer.fsproj").read_text(encoding="utf-8-sig")
m = re.search(r"<Version>([^<]+)</Version>", text)
print(m.group(1) if m else "0.1.0")
PY
)"
fi

echo "==> Pack $PACKAGE_ID $VERSION ($CONFIGURATION)"
if command -v paket >/dev/null 2>&1; then
  paket restore
fi
dotnet pack -c "$CONFIGURATION" -o "$NUPKG_OUT" \
  --nologo -v q \
  "/p:Version=$VERSION" \
  "/p:PackageVersion=$VERSION"

NUPKG="$NUPKG_OUT/${PACKAGE_ID}.${VERSION}.nupkg"
if [[ ! -f "$NUPKG" ]]; then
  # Fallback: some SDKs normalize version in the filename
  NUPKG="$(ls -1 "$NUPKG_OUT"/${PACKAGE_ID}.*.nupkg 2>/dev/null | sort -V | tail -1 || true)"
fi
if [[ -z "${NUPKG}" || ! -f "$NUPKG" ]]; then
  echo "ERROR: packed nupkg not found under $NUPKG_OUT" >&2
  exit 1
fi
echo "    nupkg → $NUPKG"

# Same version number must be wiped or NuGet will reuse stale extracted bits.
DEST="$GLOBAL_PACKAGES/$PACKAGE_ID_LOWER/$VERSION"
if [[ -d "$DEST" ]]; then
  echo "    Removing previous global package at $DEST"
  rm -rf "$DEST"
fi

# Install via restore into a throwaway project so the global packages layout
# matches a normal `dotnet add package` (nuspec, analyzers/, .nupkg, metadata).
TMP="$(mktemp -d)"
cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

echo "==> Install into NuGet global packages ($GLOBAL_PACKAGES)"
pushd "$TMP" >/dev/null
dotnet new classlib -n _fspure_analyzer_install -f net10.0 --force --language C# >/dev/null
cd _fspure_analyzer_install

cat > nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fspure-local" value="$NUPKG_OUT" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="$GLOBAL_PACKAGES" />
  </config>
</configuration>
EOF

dotnet add package "$PACKAGE_ID" --version "$VERSION" --source "$NUPKG_OUT" --package-directory "$GLOBAL_PACKAGES"
popd >/dev/null

DLL="$DEST/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
if [[ ! -f "$DLL" ]]; then
  echo "ERROR: analyzer DLL missing after install: $DLL" >&2
  exit 1
fi

echo "✅ Installed $PACKAGE_ID $VERSION"
echo "   $DLL"
echo "   Reload the window (or restart Ionide) if pure/impure badges do not refresh."
