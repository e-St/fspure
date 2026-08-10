# fspure e2e image: shared fstarter base + code-server + Playwright deps.
# Used only by tests/e2e/phase2/.devcontainer and .github/workflows/e2e-customer.yml.
#
# Shared tool base with IDE/build (ghcr.io/e-st/fstarter). This Dockerfile only
# adds e2e-specific layers — no Dev Container Features.
ARG BASE_IMAGE=ghcr.io/e-st/fstarter:latest
FROM ${BASE_IMAGE}

USER root

# System deps for Playwright browsers / screenshots (code-server UI capture)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        git \
        python3 \
        python3-pip \
        jq \
        libnss3 \
        libnspr4 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libcups2 \
        libdrm2 \
        libxkbcommon0 \
        libxcomposite1 \
        libxdamage1 \
        libxfixes3 \
        libxrandr2 \
        libgbm1 \
        libasound2t64 \
        libpango-1.0-0 \
        libcairo2 \
        fonts-liberation \
        fonts-dejavu-core \
        xdg-utils \
    && rm -rf /var/lib/apt/lists/*

# code-server = VS Code for the Web (same extension host as Codespaces)
ARG CODE_SERVER_VERSION=4.96.4
RUN curl -fsSL "https://github.com/coder/code-server/docs/releases/download/v${CODE_SERVER_VERSION}/code-server_${CODE_SERVER_VERSION}_amd64.deb" \
      -o /tmp/code-server.deb \
    && dpkg -i /tmp/code-server.deb \
    && rm -f /tmp/code-server.deb

# Phase 1 CLI analyzer. paket/node come from BASE_IMAGE (fstarter).
USER vscode
ENV PATH="/home/vscode/.dotnet/tools:${PATH}"
RUN if dotnet tool list -g 2>/dev/null | grep -q '^fsharp-analyzers'; then \
      dotnet tool update --global fsharp-analyzers --version 0.35.0 ; \
    else \
      dotnet tool install --global fsharp-analyzers --version 0.35.0 ; \
    fi

WORKDIR /workspaces/fspure
