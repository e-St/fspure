# F# Pure Analyzer Decorations

VS Code extension that visualizes [`FSharp.PureAnalyzer`](https://github.com/e-St/fspure) diagnostics.

## Appearance (default)

```fsharp
let getTimestamp () =  // unit -> DateTime          impure
    DateTime.UtcNow
```

- Light blue **box** around the impure function name  
- Boxed **`impure`** badge at the **end of the signature line** (next to Ionide type hints)

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `fsharpPureDecorations.enabled` | `true` | Toggle decorations |
| `fsharpPureDecorations.style` | `badge-end-of-line` | `badge-end-of-line` \| `badge-after-name` \| `box-name-only` |
| `fsharpPureDecorations.color` | `#4FC1FF` | Accent color |

## Install from GitHub Release

Download the latest `.vsix` from the [Releases](https://github.com/e-St/fspure/releases) page, then:

```bash
code --install-extension fsharp-pure-decorations-*.vsix
```

In a Dev Container this is handled automatically (see consumer repo `.devcontainer`).

## Requirements

- VS Code / Codespaces / Dev Containers 1.85+
- `FSharp.PureAnalyzer` producing `PURE001` / `PURE002` diagnostics
- F# language mode (`fsharp`)
