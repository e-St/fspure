> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

# fspure

This project explores how an **F# analyzer** and **VS Code extension** can help you push impurity to the boundary of your application.

It does that by defining a pure subset and marking everything else as impure.

![pure / impure decorations in the editor](image.png)

| Component | Role |
|-----------|------|
| **FSharp.PureAnalyzer** | Classifies definitions (`PURE002` impure / `PURE003` pure) for Ionide & `fsharp-analyzers` |
| **fsharp-pure-decorations** | VS Code extension: end-of-line **pure** / **impure** badges after Ionide LineLens |

---

## Consume fspure (end users)

You need **both** pieces for the full IDE experience: the analyzer produces diagnostics; the extension turns them into badges.

### Easy path — Visual Studio Marketplace + nuget.org

#### 1. Analyzer (NuGet)

```bash
dotnet add package FSharp.PureAnalyzer
```

Paket:

```
nuget FSharp.PureAnalyzer
```

The package places the analyzer DLL under `analyzers/dotnet/fs/`.

#### 2. VS Code extension (Marketplace)

In VS Code: Extensions → search **F# Pure Analyzer Decorations**, or:

```bash
code --install-extension e-st.fsharp-pure-decorations
```

Also install [Ionide for F#](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) if you do not already use it.

#### 3. Workspace settings

`.vscode/settings.json` (minimal):

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers",
    "~/.nuget/packages/fsharp.pureanalyzer"
  ],
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "// ",
  "fsharpPureDecorations.enabled": true,
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
```

Open your solution / project, open an F# file, wait for Ionide to load. You should see LineLens signatures and pure/impure badges on definitions.

> **Note:** Marketplace and nuget.org publishes require maintainer secrets (`VSCE_PAT`, `NUGET_USER` for Trusted Publishing). Until those are configured, use the advanced GitHub path below — behavior is the same.

---

### Advanced path — GitHub Releases + GitHub Packages

Same runtime model as the easy path; you install artifacts from this repository instead of the official stores.

#### Analyzer from GitHub Packages

1. Create a `nuget.config` (or user-level source) that includes GitHub Packages for this org/user, and authenticate with a PAT that has `read:packages`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github-e-st" value="https://nuget.pkg.github.com/e-St/index.json" />
  </packageSources>
</configuration>
```

```bash
dotnet nuget add source \
  "https://nuget.pkg.github.com/e-St/index.json" \
  --name github-e-st \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text

dotnet add package FSharp.PureAnalyzer --source github-e-st
```

Or download the `.nupkg` from a [GitHub Release](https://github.com/e-St/fspure/releases) and install from a local folder:

```bash
dotnet nuget add source /path/to/nupkgs --name local-fspure
dotnet add package FSharp.PureAnalyzer --source local-fspure
```

#### Extension from GitHub Release VSIX

1. Download `fsharp-pure-decorations-*.vsix` from [Releases](https://github.com/e-St/fspure/releases) (tag `vscode-extension-v*` or floating `vscode-extension-latest`).
2. Install:

```bash
code --install-extension fsharp-pure-decorations-0.2.5.vsix
```

#### Dev Container / Codespaces (project-local drop)

Useful when you want a pinned analyzer DLL next to the repo (no NuGet restore of the analyzer required):

1. Drop `FSharp.PureAnalyzer.dll` under `analyzers/dotnet/fs/` (or point `FSharp.analyzersPath` at that tree).
2. Install the extension from VSIX in `postCreateCommand` / `postAttachCommand`, for example:

```bash
code --install-extension /path/to/fsharp-pure-decorations-*.vsix
```

3. Use the same Ionide settings as above.

The in-repo e2e fixture (`e2e/customer-fixture`) is a complete minimal example of this layout.

---

### Optional: CLI-only (no VS Code)

```bash
dotnet tool install --global fsharp-analyzers
fsharp-analyzers --project YourProject.fsproj \
  --analyzers-path path/to/analyzers-or-package-root
```

---

## Known pure functions

Classification uses a list of known pure methods. If a definition only uses pure constructs/functions, it is marked pure; otherwise impure.

Currently scanned / included core surfaces include:

```
System.Private.CoreLib
System.Runtime
System.Console
System.Linq
System.Collections
System.Collections.Concurrent
System.Collections.Immutable
System.Memory
System.Threading
System.Threading.Tasks
System.Text.RegularExpressions
System.ObjectModel
System.Numerics
FSharp.Core
System.Text.Json
System.Text.Encodings.Web
System.Xml.Linq
System.Xml
System.Globalization
System.Buffers
System.IO.Pipelines
System.Runtime.CompilerServices.Unsafe
System.Runtime.InteropServices
Microsoft.Extensions.Primitives
Microsoft.Extensions.DependencyInjection.Abstractions
System.ComponentModel
System.ComponentModel.Primitives
System.ComponentModel.TypeConverter
System.Diagnostics.DiagnosticSource
System.Net.Http.Json
System.Text.Json.Serialization
```

---

## Develop this repository

- Solution: `fspure.slnx` (`FSharp.PureAnalyzer`, `purity-collector`)
- Customer e2e: [e2e/README.md](e2e/README.md)
- Maintainer publishing (secrets, Marketplace, nuget.org): [docs/PUBLISHING.md](docs/PUBLISHING.md)

```bash
# Analyzer
cd FSharp.PureAnalyzer && paket restore && dotnet build -c Release

# Extension unit tests
node vscode-extension/test/decorations.logic.test.js

# Phase 1 e2e (analyzer baseline)
bash e2e/phase1/run.sh
```

## License

MIT — see [LICENSE](LICENSE).
