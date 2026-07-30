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

The package ships the analyzer under `analyzers/dotnet/fs/`, which Ionide / `fsharp-analyzers` discover when `FSharp.analyzersPath` includes your package restore location (or a project-local `analyzers` folder).

## Ionide settings

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers",
    "~/.nuget/packages/fsharp.pureanalyzer"
  ]
}
```

Pair with the **F# Pure Analyzer Decorations** VS Code extension for end-of-line pure/impure labels.

## More

- Repo: https://github.com/e-St/fspure  
- Consumer guide: see the root [README](https://github.com/e-St/fspure#readme)
