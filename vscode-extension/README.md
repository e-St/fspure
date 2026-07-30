# F# Pure Analyzer Decorations

VS Code extension that visualizes [`FSharp.PureAnalyzer`](https://github.com/e-St/fspure) diagnostics.

## Appearance (default)

Matches the typical Ionide + skinow-style layout:

```fsharp
let add a b = // int -> int -> list<int> pure
    List.map (fun x -> x * a + b) [1; 2; 3]
```

- **No** argument type inlays (`a : int`) when `FSharp.inlayHints.typeAnnotations` is `false`
- Ionide **LineLens** provides the Hindley–Milner signature (`// int -> int -> list<int>`)
- This extension appends **`pure` / `impure` after LineLens** (end-of-line decoration)
- Driven by definition diagnostics: `PURE002` → impure, `PURE003` → pure

Recommended companion settings (skinow-style):

```json
{
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.inlayHints.enabled": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "// ",
  "FSharp.enableAnalyzers": true,
  "editor.inlayHints.enabled": "on",
  "fsharpPureDecorations.enabled": true
}
```

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `fsharpPureDecorations.enabled` | `true` | Toggle pure/impure badges |
| `fsharpPureDecorations.impureColor` | `#E2A66A` | Accent color for impure badge |
| `fsharpPureDecorations.pureColor` | `#6A9955` | Accent color for pure badge |

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
