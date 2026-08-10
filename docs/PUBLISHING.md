# Publishing fspure artifacts

This document is for **maintainers**. End-user install steps live in the root [README](../README.md).  
Architecture (embeds, overrides, precedence): [ARCHITECTURE.md](ARCHITECTURE.md).

Both distribution channels stay active:

| Artifact | Easy (registry) | Advanced (GitHub) |
|----------|-----------------|-------------------|
| VS Code extension | [Open VSX](https://open-vsx.org/) | GitHub Release `.vsix` |
| F# analyzer (+ MSBuild embed targets) | nuget.org | GitHub Packages + Release assets |
| fspure-collector (dotnet tool) | nuget.org | GitHub Packages + Release assets |

> **Note:** This project does **not** publish to the Visual Studio Marketplace (that path needs Azure DevOps / an Azure subscription). Open VSX is the default registry.

## One-time setup

### Open VSX (default extension registry)

1. Create an [Open VSX](https://open-vsx.org/) account and namespace matching `package.json` → `"publisher": "e-st"` (or change the publisher id and re-publish).
2. Create an Open VSX **access token** with publish permission for that namespace.
3. Add repository secret **`OVSX_PAT`** = that token.

### NuGet.org (Trusted Publishing / OIDC)

Do **not** use a long-lived `NUGET_API_KEY`. Publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

1. Create a [nuget.org](https://www.nuget.org/) account that will own `FSharp.PureAnalyzer`.
2. Add repository secret **`NUGET_USER`** = your nuget.org **username / profile name** (not an email, not an API key).
3. On nuget.org → your username → **Trusted Publishing**, create a policy:
   - **Repository Owner:** `e-St`
   - **Repository:** `fspure`
   - **Workflow File:** `nuget_publish.yml` (file name only — no `.github/workflows/` path)
   - **Environment:** leave empty (this repo does not use a GitHub Actions `environment:`)
4. Ensure the package id `FSharp.PureAnalyzer` is reserved under your account (first successful push claims it if free).

### GitHub (already wired)

- Extension: workflow packages a VSIX and uploads to GitHub Releases (`vscode-extension-v*` and floating `vscode-extension-latest`).
- Analyzer: workflows pack a nupkg and push to **GitHub Packages** (`nuget.pkg.github.com`).

GitHub Packages needs no extra secrets beyond `GITHUB_TOKEN` (provided by Actions).

## Workflows

| Workflow | Triggers | Publishes |
|----------|----------|-----------|
| `.github/workflows/publish-vscode-extension.yml` | Push to `vscode-extension/**` or manual | GitHub Release always; Open VSX (`OVSX_PAT` required) |
| `.github/workflows/release-pure-analyzer.yml` | GitHub Release published or manual version input | GitHub Packages + Release assets |
| `.github/workflows/nuget_publish.yml` | GitHub Release published or manual version input | **FSharp.PureAnalyzer** → nuget.org via OIDC (`NUGET_USER`); also GitHub Packages |
| `.github/workflows/publish-fspure-collector.yml` | GitHub Release published or manual version input | **fspure-collector** tool → nuget.org + GitHub Packages |

### Phase 3 analyzer on nuget.org

Library embed targets require a package that includes `build/` + `tools/fspure-collector/`.  
Ship that layout with `nuget_publish.yml` (not the older analyzer-only 0.3.2 layout). After publish, satellite `fspure-ready-lib` main CI can pin the new stable version instead of GitHub Packages `-ci.*` builds.

```bash
# Consumers
dotnet add package FSharp.PureAnalyzer --version <version>

# Optional: tool only
dotnet tool install -g fspure-collector --version <version>
```

`publish-vscode-extension.yml` **fails** if `OVSX_PAT` is missing. `nuget_publish.yml` **fails** if `NUGET_USER` is missing (Trusted Publishing cannot mint a temp key without it).

`nuget_publish.yml` uses `actions/setup-dotnet` + global `paket` on `ubuntu-latest` (no devcontainer). Analyzer pack/build workflows that do use a container pin `FSharp.PureAnalyzer/.devcontainer/devcontainer.json` (not the interactive IDE container).

## Versioning

- **Extension:** bump `vscode-extension/package.json` → `version` before merge (Open VSX rejects reusing a version).
- **Analyzer:** pass version via release tag or `workflow_dispatch` input; do not re-use published nuget.org versions.

## Local dry-runs

```bash
# Extension VSIX
cd vscode-extension
npx --yes @vscode/vsce package

# Analyzer nupkg
cd FSharp.PureAnalyzer
paket restore && dotnet build -c Release
dotnet pack -c Release -o ./nupkgs /p:Version=0.0.0-local
```
