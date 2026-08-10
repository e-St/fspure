<!-- GENERATED FILE — do not edit by hand.
     Source: .devcontainer/fragments/vscode-common.json
     Regenerate: python3 .devcontainer/generate-customer-md.py -->

# Using fspure in your project
This guide is for **end users** who want pure/impure labels in the editor. It is not for contributors to the fspure repository.
For the full IDE experience you need **both**:
1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions (`PURE002` / `PURE003`)
2. **fsharp-pure-decorations** (VS Code extension) — shows end-of-line **pure** / **impure** badges after Ionide LineLens
Plus **Ionide for F#** (language service that loads the analyzer). How you get them depends on which path you pick:
| Path | When to use |
|------|-------------|
| **§0 e-St/fstarter** | You want an opinionated F# Codespace / dev container that already includes fspure |
| **§1 No dev container** | Local VS Code / desktop IDE; you wire NuGet + extension yourself |
| **§2 Your own dev container** | You already have (or want) a project `.devcontainer/` and will add fspure to it |
## 0. Use e-St/fstarter (recommended if you want zero setup)
[**e-St/fstarter**](https://github.com/e-St/fstarter) is an opinionated F# Codespace / dev-container starter. It already delivers the F# toolchain (Ionide, .NET, Paket, etc.) **and fspure** (analyzer + pure/impure decorations) so you do not install packages or extensions by hand for labels to work.
### What you do
1. Open or create a project from [e-St/fstarter](https://github.com/e-St/fstarter) (GitHub Codespaces “Open in codespace”, or clone and “Reopen in Container”).
2. Work on your F# code inside that environment.
3. Open a solution / `.fs` file and wait for Ionide — pure/impure badges should appear without further fspure configuration.
### When this is the right choice
- You are starting greenfield F# work and are fine with the fstarter defaults.
- You want Codespaces / a full F# container, not a minimal local install.
- You do not want to maintain NuGet paths, extension installs, or Ionide settings yourself.
### When to use §1 or §2 instead
- Your app already lives in its own repo with its own tooling and you only want fspure added (§1 or §2).
- You cannot use GitHub Codespaces / that base image (air-gapped, corporate base image, etc.).
Details of what fstarter pins (image tags, setup scripts) live in the [fstarter repository](https://github.com/e-St/fstarter) — treat that as the source of truth for the starter itself.
## 1. Usage without a dev container
### 1.1 Install the analyzer (NuGet)
```bash
dotnet add package FSharp.PureAnalyzer
```
Or with Paket:
```
nuget FSharp.PureAnalyzer
```
The package ships the analyzer under `analyzers/dotnet/fs/`. Ionide’s FSAC must see a **real directory** via `FSharp.analyzersPath` (it does **not** expand `~`, `${userHome}`, or other VS Code variables).
Typical approaches:
- Point `FSharp.analyzersPath` at a workspace folder such as `analyzers` and copy/symlink the package’s `analyzers/dotnet/fs/` tree there, or
- Use an **absolute** path to the installed package under your NuGet global packages folder.
### 1.2 Install the VS Code extension
The extension is published to [Open VSX](https://open-vsx.org/) as `e-st.fsharp-pure-decorations` (**F# Pure Analyzer Decorations**).
- **Open VSX clients** (VSCodium, many code-server setups, Cursor with Open VSX): search and install from the marketplace.
- **Stock VS Code** (Microsoft Marketplace): install a `.vsix` from [GitHub Releases](https://github.com/e-St/fspure/releases), or configure Open VSX.
```bash
code --install-extension e-st.fsharp-pure-decorations
# or from a downloaded VSIX:
code --install-extension fsharp-pure-decorations-*.vsix
```
Also install **Ionide for F#** (`ionide.ionide-fsharp`) if you do not already use it.
### 1.3 Workspace settings
Add (or merge) into `.vscode/settings.json`.
#### Required
```json
{
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers"
  ],
  "FSharp.enableAnalyzers": true,
  "fsharpPureDecorations.enabled": true
}
```
#### Recommended
These make LineLens signatures and pure/impure badges readable (badges sit after `// signature`; grey diagnostic hint text is hidden).
```json
{
  "FSharp.inlayHints.enabled": true,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "editor.inlayHints.enabled": "on",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
```
#### Combined minimum (required + recommended)
```json
{
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers"
  ],
  "FSharp.enableAnalyzers": true,
  "fsharpPureDecorations.enabled": true,
  "FSharp.inlayHints.enabled": true,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "editor.inlayHints.enabled": "on",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
```
#### Optional
Useful editor UX from our reference setup; not required for classification or badges. Decorations colors default to impure orange / pure green.
```json
{
  "FSharp.codeLenses.references.enabled": false,
  "FSharp.enableMSBuildProjectGraph": true,
  "FSharp.linter": true,
  "FSharp.pipelineHints.enabled": true,
  "FSharp.pipelineHints.prefix": "  // ",
  "FSharp.unusedDeclarationsAnalyzer": true,
  "[fsharp]": {
    "editor.quickSuggestions": false,
    "editor.suggestOnTriggerCharacters": false
  },
  "editor.acceptSuggestionOnEnter": "off",
  "editor.formatOnSave": true,
  "editor.inlineSuggest.enabled": false,
  "editor.parameterHints.enabled": false,
  "files.exclude": {
    "**/obj": true,
    "**/bin": true,
    "**/.paket": true
  },
  "fsharpPureDecorations.impureColor": "#E2A66A",
  "fsharpPureDecorations.pureColor": "#6A9955"
}
```
### 1.4 Open your solution
Open the solution or project, open an `.fs` file, wait for Ionide to load. You should see LineLens signatures (`// …`) and **pure** / **impure** badges on definitions. If labels are missing: **Developer: Reload Window**, and confirm the analyzer DLL is under a path listed in `FSharp.analyzersPath`.
## 2. Usage with a dev container (opinionated)
If you develop in a [dev container](https://containers.dev/), follow this **one recipe**. It is the full recommended IDE stack for pure/impure labels — not a menu of options. Required / recommended / optional settings are already listed in §1; this section only tells you what to put in **your** `.devcontainer/`.
You do **not** need this repository’s internal IDE, build, or e2e containers.
### 2.1 What you commit
1. `FSharp.PureAnalyzer` as a normal NuGet or Paket dependency on your F# project.
2. `.devcontainer/devcontainer.json` — use the template below (only change the two solution path strings).
3. `.devcontainer/setup-fspure.sh` — use the script below as-is.
Regenerate `analyzers/dotnet/fs/FSharp.PureAnalyzer.dll` (and `FSharp.PureSchema.dll`) on every create/attach via the setup script; you normally do **not** commit that drop.
### 2.2 `.devcontainer/devcontainer.json`
Swap in your base image if you already have one. Keep `postCreateCommand`, `postAttachCommand`, and `customizations.vscode` as shown. Replace `YourSolution.sln` with your solution (or `.slnx`).
```json
{
  "name": "My F# app + fspure",
  "image": "mcr.microsoft.com/devcontainers/dotnet:1-10.0-noble",
  "remoteUser": "vscode",
  "postCreateCommand": "bash .devcontainer/setup-fspure.sh",
  "postAttachCommand": "bash .devcontainer/setup-fspure.sh",
  "customizations": {
    "vscode": {
      "extensions": [
        "ionide.ionide-fsharp",
        "e-st.fsharp-pure-decorations",
        "ionide.ionide-paket",
        "ionide.ionide-fantomas",
        "ms-dotnettools.csharp"
      ],
      "settings": {
        "FSharp.analyzersPath": [
          "analyzers",
          "packages/Analyzers"
        ],
        "FSharp.codeLenses.references.enabled": false,
        "FSharp.enableAnalyzers": true,
        "FSharp.enableMSBuildProjectGraph": true,
        "FSharp.inlayHints.enabled": true,
        "FSharp.inlayHints.parameterNames": true,
        "FSharp.inlayHints.typeAnnotations": false,
        "FSharp.lineLens.enabled": "replaceCodeLens",
        "FSharp.lineLens.prefix": "  // ",
        "FSharp.linter": true,
        "FSharp.pipelineHints.enabled": true,
        "FSharp.pipelineHints.prefix": "  // ",
        "FSharp.unusedDeclarationsAnalyzer": true,
        "[fsharp]": {
          "editor.quickSuggestions": false,
          "editor.suggestOnTriggerCharacters": false
        },
        "editor.acceptSuggestionOnEnter": "off",
        "editor.formatOnSave": true,
        "editor.inlayHints.enabled": "on",
        "editor.inlineSuggest.enabled": false,
        "editor.parameterHints.enabled": false,
        "files.exclude": {
          "**/obj": true,
          "**/bin": true,
          "**/.paket": true
        },
        "fsharpPureDecorations.enabled": true,
        "workbench.colorCustomizations": {
          "editorHint.foreground": "#00000000",
          "editorHint.border": "#00000000",
          "editorOverviewRuler.hintForeground": "#00000000"
        },
        "dotnet.defaultSolution": "YourSolution.sln",
        "FSharp.workspacePath": "YourSolution.sln"
      }
    }
  }
}
```
### 2.3 `.devcontainer/setup-fspure.sh`
Runs on create and attach: installs the decorations extension when `code` is available, and mirrors the restored NuGet analyzer into workspace `analyzers/` so `FSharp.analyzersPath` works (FSAC does not expand home/`~`).
```bash
#!/usr/bin/env bash
set -euo pipefail

# Codespaces uses MS Marketplace — extension is on Open VSX only.
# Install from downloaded VSIX (not marketplace id alone).
if command -v code >/dev/null 2>&1; then
  vsix=$(mktemp --suffix=.vsix)
  url=$(curl -fsSL https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['files']['download'])")
  curl -fsSL -o "$vsix" "$url"
  code --install-extension "$vsix" --force
  rm -f "$vsix"
fi

# Mirror NuGet analyzer + PureSchema into workspace-relative analyzers/ for FSAC.
PKG="${NUGET_PACKAGES:-$HOME/.nuget/packages}/fsharp.pureanalyzer"
DLL="$(find "$PKG" -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' \
  2>/dev/null | sort -V | tail -1 || true)"
if [[ -z "${DLL}" || ! -f "${DLL}" ]]; then
  echo "FSharp.PureAnalyzer DLL not found under $PKG — restore the package first." >&2
  exit 1
fi
SCHEMA="$(dirname "$DLL")/FSharp.PureSchema.dll"
mkdir -p analyzers/dotnet/fs
cp -f "$DLL" analyzers/dotnet/fs/FSharp.PureAnalyzer.dll
if [[ -f "$SCHEMA" ]]; then
  cp -f "$SCHEMA" analyzers/dotnet/fs/FSharp.PureSchema.dll
else
  echo "WARNING: FSharp.PureSchema.dll missing next to analyzer (older package?)." >&2
fi
echo "PureAnalyzer → analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"
```
Make it executable (`chmod +x .devcontainer/setup-fspure.sh`). Restore your project (so the NuGet package is present) before or at the start of this script. After attach: open the solution, open an `.fs` file, wait for Ionide. If badges are missing, **Developer: Reload Window**.
---
## See also
- [README — consume fspure](README.md#consume-fspure-end-users)
- [FSharp.PureAnalyzer](src/FSharp.PureAnalyzer/README.md)
- [VS Code extension](editor/vscode-extension/README.md)
- Maintainer publishing: [docs/PUBLISHING.md](docs/PUBLISHING.md)
