# F# Pure Analyzer Decorations

VS Code extension that visualizes [`FSharp.PureAnalyzer`](https://github.com/e-St/fspure) diagnostics as end-of-line **pure** / **impure** badges.

## Appearance

```fsharp
let add a b = // int -> int -> list<int> pure
    List.map (fun x -> x * a + b) [1; 2; 3]
```

- Ionide **LineLens** shows the Hindley–Milner signature (`// int -> int -> list<int>`)
- This extension appends **`pure` / `impure` after LineLens**
- Driven by definition diagnostics: `PURE002` → impure, `PURE003` → pure

Recommended companion settings:

```json
{
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.inlayHints.enabled": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "FSharp.enableAnalyzers": true,
  "editor.inlayHints.enabled": "on",
  "fsharpPureDecorations.enabled": true
}
```

## Install

### Easy — Open VSX

Published to [Open VSX](https://open-vsx.org/) (default registry for this project; not the Visual Studio Marketplace).

In **VSCodium**, **code-server**, or any client that uses Open VSX: Extensions → search **F# Pure Analyzer Decorations** (`e-st.fsharp-pure-decorations`).

### Advanced — GitHub Release VSIX

Download the latest `.vsix` from [Releases](https://github.com/e-St/fspure/releases), then:

```bash
code --install-extension fsharp-pure-decorations-*.vsix
```

This is the straightforward path for stock VS Code (Microsoft Marketplace). Dev Containers can install the same VSIX in `postCreateCommand` / `postAttachCommand`.

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `fsharpPureDecorations.enabled` | `true` | Toggle pure/impure badges |
| `fsharpPureDecorations.impureColor` | `#E2A66A` | Accent color for impure badge |
| `fsharpPureDecorations.pureColor` | `#6A9955` | Accent color for pure badge |

## Requirements

- VS Code / Codespaces / Dev Containers / code-server 1.85+
- [Ionide](https://open-vsx.org/extension/Ionide/Ionide-fsharp) (or compatible FSAC host)
- `FSharp.PureAnalyzer` producing `PURE002` / `PURE003` (NuGet or project-local drop)

Full consumer guide: [fspure README](https://github.com/e-St/fspure#readme)
