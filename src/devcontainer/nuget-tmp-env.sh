#!/usr/bin/env bash
# Source this before any `dotnet` / NuGet command.
# NuGet creates $TMPDIR/NuGetScratch* with mode 700; some Docker / CI /tmp mounts
# reject that chmod (errno=1 → "User is unable to set permission to 700").
# Workflows already export TMPDIR under $HOME; keep interactive postCreate safe too.
export TMPDIR="${TMPDIR:-${HOME}/.cache/nuget-tmp}"
export TEMP="${TEMP:-$TMPDIR}"
export TMP="${TMP:-$TMPDIR}"
mkdir -p "$TMPDIR"
