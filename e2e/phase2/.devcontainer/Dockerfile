# fspure e2e image: .NET + Node + code-server (VS Code Web) + Playwright deps.
# Used only by e2e/phase2/.devcontainer and .github/workflows/e2e-customer.yml.
# Interactive IDE: root .devcontainer/ ("fspure IDE") — do not merge the two.
FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

USER root

# System deps for code-server, Playwright browsers, and screenshots
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

# Node.js 20 (Playwright driver)
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# code-server = VS Code for the Web (same extension host as Codespaces)
# Pins a recent stable; bump when needed.
ARG CODE_SERVER_VERSION=4.96.4
RUN curl -fsSL "https://github.com/coder/code-server/releases/download/v${CODE_SERVER_VERSION}/code-server_${CODE_SERVER_VERSION}_amd64.deb" \
      -o /tmp/code-server.deb \
    && dpkg -i /tmp/code-server.deb \
    && rm -f /tmp/code-server.deb

# Global tooling used by Phase 1/2 scripts
USER vscode
ENV PATH="/home/vscode/.dotnet/tools:${PATH}"
RUN dotnet tool install --global paket \
    && dotnet tool install --global fsharp-analyzers --version 0.35.0

WORKDIR /workspaces/fspure
