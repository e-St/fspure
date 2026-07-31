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
| `.github/workflows/publish-vscode-extension.yml` | Push to `vscode-extension/**` or manual | GitHub Release always; Marketplace if `VSCE_PAT`; Open VSX if `OVSX_PAT` |
| `.github/workflows/release-pure-analyzer.yml` | GitHub Release published or manual version input | GitHub Packages + Release assets |
| `.github/workflows/nuget_publish.yml` | GitHub Release published or manual version input | nuget.org via OIDC (`NUGET_USER` required); also GitHub Packages + Release assets |

Missing Marketplace / Open VSX secrets **skip** those steps without failing the job. `nuget_publish.yml` **fails** if `NUGET_USER` is missing (Trusted Publishing cannot mint a temp key without it).

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
