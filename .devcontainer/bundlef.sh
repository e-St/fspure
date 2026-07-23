#!/bin/bash
set -e

PUBLISH_ONLY=false

for arg in "$@"; do
  case "$arg" in
    -p) PUBLISH_ONLY=true ;;
  esac
done

FSPROJ=$(ls *.fsproj 2>/dev/null | head -1)
if [ -z "$FSPROJ" ]; then
  echo "Error: no .fsproj found in current directory. Run bundlef from your project folder."
  exit 1
fi

PROJ_NAME="${FSPROJ%.fsproj}"

if [ -d ".elmish-land" ]; then
  echo "Detected .elmish-land — running dotnet elmish-land build..."
  dotnet tool restore
  npm install
  dotnet elmish-land build

  DIST_DIR="$(pwd)/dist"
  if [ ! -d "$DIST_DIR" ]; then
    echo "Error: dist directory not found after build."
    exit 1
  fi

  if $PUBLISH_ONLY; then
    echo "$DIST_DIR"
  else
    echo "Serving $DIST_DIR at http://localhost:8080 ..."
    python3 -m http.server 8080 --directory "$DIST_DIR"
  fi
  exit 0
fi

dotnet publish -r linux-x64 --self-contained true -c Release -p:PublishSingleFile=true

BINARY=$(find bin/Release -path "*/linux-x64/publish/${PROJ_NAME}" -type f 2>/dev/null | head -1)

if [ -z "$BINARY" ]; then
  echo "Error: could not locate the published binary."
  exit 1
fi

BINARY_ABS=$(realpath "$BINARY")

if $PUBLISH_ONLY; then
  echo "$BINARY_ABS"
else
  echo "Running: $BINARY_ABS"
  "$BINARY_ABS"
fi
