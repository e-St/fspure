# Publishing fspure artifacts

This document is for **maintainers**. End-user install steps live in the root [README](../README.md).  
Architecture (embeds, overrides, precedence): see the generated architecture page on [fspure.net](https://fspure.net/) (template: [`templates/ARCHITECTURE.md.scriban`](templates/ARCHITECTURE.md.scriban)).

Both distribution channels stay active:

| Artifact | Easy (registry) | Advanced (GitHub) |
|----------|-----------------|-------------------|
| VS Code extension | [Open VSX](https://open-vsx.org/) | GitHub Release `.vsix` |
| F# analyzer (+ MSBuild embed targets) | nuget.org | GitHub Packages + Release assets |
| fspure-collector (dotnet tool) | nuget.org | GitHub Packages + Release assets |
| fspure (`analyze` CLI + standalone) | nuget.org | GitHub Packages + `fspure-latest` Release asset |

> **Note:** This project does **not** publish to the Visual Studio Marketplace (that path needs Azure DevOps / an Azure subscription). Open VSX is the default registry.

## One-time setup

### Open VSX (default extension registry)

1. Create an [Open VSX](https://open-vsx.org/) account and namespace matching `package.json` → `"publisher": "e-st"` (or change the publisher id and re-publish).
2. Create an Open VSX **access token** with publish permission for that namespace.
3. Add repository secret **`OVSX_PAT`** = that token.

### NuGet.org (Trusted Publishing / OIDC)

Do **not** use a long-lived `NUGET_API_KEY`. Publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

1. Create a [nuget.org](https://www.nuget.org/) account that will own `FSharp.PureAnalyzer` and `fspure-collector`.
2. Add repository secret **`NUGET_USER`** = the nuget.org **username of the policy creator** (profile name — not an email, not an API key, not necessarily the “owner” display if different).
3. On nuget.org → your username → **Trusted Publishing**, create **one** policy:
   - **Repository Owner:** `e-St`
   - **Repository:** `fspure`
   - **Workflow File:** `nuget_publish.yml` (file name only — no `.github/workflows/` path)
   - **Environment:** leave empty (this repo does not use a GitHub Actions `environment:`)
4. First successful push claims free package ids under your account.

**Important:** NuGet matches the **exact workflow file name**.  
A policy for `nuget_publish.yml` will **not** authorize `publish-fspure-collector.yml` (HTTP 401 “Workflow mismatch”).  
That is why **both** packages publish from **`nuget_publish.yml` only**.

### GitHub (already wired)

- Extension: workflow packages a VSIX and uploads to GitHub Releases.
- Analyzer CI prereleases: `Publish analyzer to GitHub Packages (CI)` on main.
- Collector-only to GitHub Packages: `Publish fspure-collector tool (GitHub Packages)` (no nuget.org).

## Workflows

| Workflow | Triggers | Publishes |
|----------|----------|-----------|
| `.github/workflows/publish-vscode-extension.yml` | Push to `src/editor/vscode-extension/**` or manual | GitHub Release; Open VSX (`OVSX_PAT`) |
| `.github/workflows/release-pure-analyzer.yml` | Release / manual | Analyzer → GitHub Packages + Release assets |
| **`.github/workflows/nuget_publish.yml`** | Release / manual | **FSharp.PureAnalyzer + fspure-collector** → **nuget.org** (OIDC) + GitHub Packages + **GitHub Release assets** (creates/updates `v{version}` and marks **Latest**) |
| `.github/workflows/publish-fspure-collector.yml` | Manual | fspure-collector → **GitHub Packages only** |

### After a nuget.org release → update fstarter

Workflow **PR fspure updates to fstarter** opens a PR on [e-St/fstarter](https://github.com/e-St/fstarter) with the integration pack and analyzer pin.  
Secret: **`FSPURE_FSTARTER_TOKEN`**. See [SYNC-FSTARTER.md](SYNC-FSTARTER.md).

### How to publish to nuget.org (use this)

**Actions → Publish Pure Analyzer to nuget.org → Run workflow**

| Input | Example | Meaning |
|-------|---------|---------|
| `version` | `0.4.0` | FSharp.PureAnalyzer (must include Phase 3 `build/` + `tools/fspure-collector/`) |
| `collector_version` | `0.1.0` | fspure-collector tool |
| `publish_collector` | true | Pack/push the tool as well |

```bash
# After a successful run
dotnet add package FSharp.PureAnalyzer --version 0.4.0
dotnet tool install -g fspure-collector --version 0.1.0
```

Library embed targets require a package that includes `build/` + `tools/fspure-collector/` (not nuget.org `0.3.2`, which is analyzer-only).

`publish-vscode-extension.yml` **fails** if `OVSX_PAT` is missing.  
`nuget_publish.yml` **fails** if `NUGET_USER` is missing or the Trusted Publishing policy workflow name does not match.

## Security automation

Dependabot, CodeQL, dependency review, NuGet/npm audits, and gitleaks: [SECURITY.md](SECURITY.md).

## Versioning

- **Extension:** bump `src/editor/vscode-extension/package.json` → `version` before merge.
- **Analyzer / collector:** do not re-use published nuget.org versions.
- Analyzer and collector versions are independent (`0.4.0` vs `0.1.0` is fine).

## Local dry-runs

```bash
# Extension VSIX
cd vscode-extension
npx --yes @vscode/vsce package

# Analyzer nupkg (Phase 3 layout)
cd FSharp.PureAnalyzer
paket restore
dotnet pack -c Release -o ./nupkgs /p:Version=0.0.0-local
unzip -l ./nupkgs/*.nupkg | grep -E 'build/|tools/fspure-collector'

# Collector tool nupkg
cd ../fspure-collector
paket restore
dotnet pack -c Release -o ./nupkgs /p:Version=0.0.0-local
```
