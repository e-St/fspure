#!/bin/bash
set -e

if [ -d ".elmish-land" ]; then
  dotnet tool restore
  npm install
  dotnet elmish-land server
elif ls *.fsproj &>/dev/null; then
  dotnet watch run
else
  echo "Error: no .fsproj or .elmish-land folder found in current directory. Run watchf from your project folder."
  exit 1
fi
