# F# Pure Analyzer Decorations

VS Code extension that visualizes [`FSharp.PureAnalyzer`](https://github.com/e-St/fspure) diagnostics.

## Appearance (default)

```fsharp
let getTimestamp () =  // … Ionide type hint …  impure
    DateTime.UtcNow
```

- **`pure` / `impure` badges** at the **end of the definition line**
- Rendered as **inlay hints** so they sit **after** Ionide type/parameter annotations (not in front of them)
- Driven by definition diagnostics: `PURE002` → impure, `PURE003` → pure

If `editor.inlayHints.enabled` is off, the extension falls back to classic end-of-line text decorations.

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `fsharpPureDecorations.enabled` | `true` | Toggle pure/impure badges |
| `fsharpPureDecorations.impureColor` | `#E2A66A` | Accent for decoration fallback (impure) |
| `fsharpPureDecorations.pureColor` | `#6A9955` | Accent for decoration fallback (pure) |

Also requires:

- `editor.inlayHints.enabled`: `on` (preferred path)
- `FSharp.enableAnalyzers`: `true` and a valid `FSharp.analyzersPath`

## Install from GitHub Release

Download the latest `.vsix` from the [Releases](https://github.com/e-St/fspure/releases) page, then:

```bash
code --install-extension fsharp-pure-decorations-*.vsix
```

In a Dev Container this is handled automatically (see consumer repo `.devcontainer`).

## Requirements

- VS Code / Codespaces / Dev Containers / code-server 1.85+
- `FSharp.PureAnalyzer` producing `PURE002` / `PURE003` definition diagnostics
- F# language mode (`fsharp`) and Ionide (or compatible FSAC host) with analyzers enabled
