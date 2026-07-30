# Publishing fspure artifacts

This document is for **maintainers**. End-user install steps live in the root [README](../README.md).

Both distribution channels stay active:

| Artifact | Easy (official) | Advanced (GitHub) |
|----------|-----------------|-------------------|
| VS Code extension | Visual Studio Marketplace (+ optional Open VSX) | GitHub Release `.vsix` |
| F# analyzer | nuget.org | GitHub Packages + Release assets |

## One-time setup

### VS Code Marketplace

1. Create a [Visual Studio Marketplace publisher](https://marketplace.visualstudio.com/manage) matching `package.json` → `"publisher": "e-st"` (or change the publisher id and re-publish).
2. Create an Azure DevOps **Personal Access Token** with **Marketplace → Manage** scope.
3. Add repository secret **`VSCE_PAT`** = that token.
4. (Optional) For VSCodium / code-server registries, create an [Open VSX](https://open-vsx.org/) token and secret **`OVSX_PAT`**.

### NuGet.org

1. Create a [nuget.org](https://www.nuget.org/) account and API key with **Push** permission for `FSharp.PureAnalyzer`.
2. Add repository secret **`NUGET_API_KEY`**.
3. Ensure the package id `FSharp.PureAnalyzer` is reserved under your account (first successful push claims it if free).

### GitHub (already wired)

- Extension: workflow packages a VSIX and uploads to GitHub Releases (`vscode-extension-v*` and floating `vscode-extension-latest`).
- Analyzer: workflow packs a nupkg and pushes to **GitHub Packages** (`nuget.pkg.github.com`).

No extra secrets beyond `GITHUB_TOKEN` (provided by Actions).

## Workflows

| Workflow | Triggers | Publishes |
|----------|----------|-----------|
| `.github/workflows/publish-vscode-extension.yml` | Push to `vscode-extension/**` or manual | GitHub Release always; Marketplace if `VSCE_PAT`; Open VSX if `OVSX_PAT` |
| `.github/workflows/release-pure-analyzer.yml` | GitHub Release published or manual version input | GitHub Packages always; nuget.org if `NUGET_API_KEY` |

Secrets are optional: missing Marketplace / nuget.org keys **skip** those steps without failing the job, so GitHub-only publishing keeps working.

## Versioning

- **Extension:** bump `vscode-extension/package.json` → `version` before merge (Marketplace rejects reusing a version).
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
