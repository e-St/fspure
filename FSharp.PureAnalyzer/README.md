# FSharp.PureAnalyzer

F# analyzer that classifies definitions as **pure** or **impure** using a known-pure method set (BCL + FSharp.Core surface).

| Diagnostic | Meaning |
|------------|---------|
| **PURE002** | Definition is not transitively pure → editor **impure** badge |
| **PURE003** | Definition is transitively pure → editor **pure** badge |
| **PURE001** | Call-site hint (no end-of-line badge) |

## Install (NuGet)

```bash
dotnet add package FSharp.PureAnalyzer
```

Or Paket:

```
nuget FSharp.PureAnalyzer
```

The package ships the analyzer under `analyzers/dotnet/fs/`, which Ionide / `fsharp-analyzers` discover when `FSharp.analyzersPath` points at a real folder that contains that layout (or the DLL tree itself). **FSAC does not expand `${userHome}` or `~`** — prefer a workspace-relative `analyzers/` drop or an absolute path.

### Local dev: install / refresh this checkout

```bash
bash FSharp.PureAnalyzer/update-analyzer.sh
```

Packs the current tree, drops the DLL at `<repo>/analyzers/dotnet/fs/` (Ionide’s default path entry `"analyzers"`), and installs into the NuGet global packages folder. Reload the VS Code window afterward so Ionide picks up the new DLL.

## Ionide settings

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers"
  ]
}
```

Pair with the **F# Pure Analyzer Decorations** VS Code extension for end-of-line pure/impure labels.

## More

- Repo: https://github.com/e-St/fspure  
- Consumer guide: see the root [README](https://github.com/e-St/fspure#readme)
