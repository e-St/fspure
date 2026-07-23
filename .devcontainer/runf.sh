#!/bin/bash
set -e

if ! ls *.fsproj &>/dev/null; then
  echo "Error: no .fsproj found in current directory. Run runf from your project folder."
  exit 1
fi

if [ ! -f build.fsx ]; then
  echo "Error: no build.fsx found in current directory."
  exit 1
fi

dotnet fsi build.fsx
dotnet run