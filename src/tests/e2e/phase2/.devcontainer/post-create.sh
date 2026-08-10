#!/usr/bin/env bash
# E2E host setup without a repo Dockerfile (uses fstarter base image + this script).
# Installs code-server and Playwright system/browser deps for visual capture.
set -euo pipefail

CODE_SERVER_VERSION="${CODE_SERVER_VERSION:-4.96.4}"

echo "==> e2e post-create: ensure fsharp-analyzers global tool"
if ! dotnet tool list -g 2>/dev/null | grep -q '^fsharp-analyzers'; then
  dotnet tool install -g fsharp-analyzers --version 0.35.0 || true
fi

if ! command -v code-server >/dev/null 2>&1; then
  echo "==> install code-server ${CODE_SERVER_VERSION}"
  if command -v sudo >/dev/null 2>&1; then
    SUDO=sudo
  else
    SUDO=
  fi
  curl -fsSL "https://github.com/coder/code-server/releases/download/v${CODE_SERVER_VERSION}/code-server_${CODE_SERVER_VERSION}_amd64.deb" \
    -o /tmp/code-server.deb
  $SUDO dpkg -i /tmp/code-server.deb || $SUDO apt-get install -f -y
  rm -f /tmp/code-server.deb
fi

# Playwright browser deps (Debian/Ubuntu). Best-effort on non-apt hosts.
if command -v apt-get >/dev/null 2>&1; then
  echo "==> apt packages for Playwright / Chromium"
  if command -v sudo >/dev/null 2>&1; then
    sudo apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      libnss3 libnspr4 libatk-bridge2.0-0 libatk1.0-0 libcups2 libdrm2 \
      libxkbcommon0 libxcomposite1 libxdamage1 libxfixes3 libxrandr2 \
      libgbm1 libasound2t64 libpango-1.0-0 libcairo2 \
      fonts-liberation fonts-dejavu-core ca-certificates curl git jq \
      || true
  fi
fi

echo "==> e2e post-create done"
