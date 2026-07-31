#!/usr/bin/env bash
# Install latest FSharp.PureAnalyzer from nuget.org into the global packages folder.
set -euo pipefail

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
cd "$tmp"
dotnet new classlib -n install -f net10.0 --force
cd install
dotnet add package FSharp.PureAnalyzer
