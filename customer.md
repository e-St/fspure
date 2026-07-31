<!-- GENERATED FILE — do not edit by hand.
     Source: .devcontainer/fragments/vscode-common.json
     Regenerate: python3 .devcontainer/generate-customer-md.py -->

# Using fspure in your project
This guide is for **end users** who want pure/impure labels in the editor. It is not for contributors to the fspure repository.
You need **both**:
1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions (`PURE002` / `PURE003`)
2. **fsharp-pure-decorations** (VS Code extension) — shows end-of-line **pure** / **impure** badges after Ionide LineLens
Plus **Ionide for F#** (language service that loads the analyzer).
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
  "FSharp.lineLens.prefix": "// ",
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
  "FSharp.lineLens.prefix": "// ",
  "editor.inlayHints.enabled": "on",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
```
#### Optional
Useful editor UX from our reference setup; not required for classification or badges. Decoration colors default to impure orange / pure green.
```json
{
  "FSharp.codeLenses.references.enabled": false,
  "FSharp.enableMSBuildProjectGraph": true,
  "FSharp.linter": true,
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
## 2. Usage with a dev container
Use this when your project already (or will) develop inside a [Development Container](https://containers.dev/). You add fspure pieces to **your** `devcontainer.json` — you do not need this repository’s internal IDE/build/e2e containers.
### 2.1 Extensions
Under `customizations.vscode.extensions`, install at least:
```json
[
  "ionide.ionide-fsharp",
  "e-st.fsharp-pure-decorations"
]
```
Optional companion extensions we use in reference setups:
```json
[
  "ionide.ionide-paket",
  "ionide.ionide-fantomas",
  "ms-dotnettools.csharp"
]
```
If `e-st.fsharp-pure-decorations` is not on the Marketplace your client uses, install the VSIX in `postCreateCommand` (see below) instead of listing the id.
### 2.2 Settings
Put the same **required + recommended** settings under `customizations.vscode.settings` (or in a workspace `.vscode/settings.json` mounted into the container).
Example `customizations` block (replace `YourSolution.sln`):
```json
{
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
        "FSharp.enableAnalyzers": true,
        "fsharpPureDecorations.enabled": true,
        "FSharp.inlayHints.enabled": true,
        "FSharp.inlayHints.parameterNames": true,
        "FSharp.inlayHints.typeAnnotations": false,
        "FSharp.lineLens.enabled": "replaceCodeLens",
        "FSharp.lineLens.prefix": "// ",
        "editor.inlayHints.enabled": "on",
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
### 2.3 Analyzer package in the container
Restore/add the NuGet package as part of your normal project restore, **and** ensure Ionide can load the DLL from a workspace-relative path.
Minimal `postCreateCommand` sketch:
```bash
# After your project restore (dotnet/paket):
dotnet add path/to/YourProject.fsproj package FSharp.PureAnalyzer

# Mirror analyzer into workspace so FSharp.analyzersPath: ["analyzers"] works:
PKG="$HOME/.nuget/packages/fsharp.pureanalyzer"
DLL="$(find "$PKG" -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' \
  2>/dev/null | sort -V | tail -1)"
mkdir -p analyzers/dotnet/fs
cp -f "$DLL" analyzers/dotnet/fs/FSharp.PureAnalyzer.dll
```
Alternatively, install from Open VSX / VSIX in the same script:
```bash
code --install-extension e-st.fsharp-pure-decorations --force
# or: code --install-extension /path/to/fsharp-pure-decorations-*.vsix --force
```
Run install steps again in `postAttachCommand` if the `code` CLI is only available after the editor attaches.
### 2.4 Skeleton `devcontainer.json`
Illustrative only — keep your own base image and features; merge the fspure-related parts:
```json
{
  "name": "My F# app + fspure",
  "image": "mcr.microsoft.com/devcontainers/dotnet:1-10.0-noble",
  "remoteUser": "vscode",
  "postCreateCommand": "bash .devcontainer/setup-fspure-customer.sh",
  "customizations": {
    "vscode": {
      "extensions": [
        "ionide.ionide-fsharp",
        "e-st.fsharp-pure-decorations"
      ],
      "settings": {
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
        "FSharp.lineLens.prefix": "// ",
        "editor.inlayHints.enabled": "on",
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
Point `dotnet.defaultSolution` / `FSharp.workspacePath` at **your** solution file. Implement `setup-fspure-customer.sh` with restore + analyzer mirror + optional VSIX install as in §2.3.
---
## See also
- [README — consume fspure](README.md#consume-fspure-end-users)
- [FSharp.PureAnalyzer](FSharp.PureAnalyzer/README.md)
- [VS Code extension](vscode-extension/README.md)
- Maintainer publishing: [docs/PUBLISHING.md](docs/PUBLISHING.md)
